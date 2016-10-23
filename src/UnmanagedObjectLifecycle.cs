/*
 * This module allows developers to "register" with a given scope unmanaged objects represented by <THandleType> type.
 * Typically this type will be IntPtr, but will work well if underlying library uses 32 or 64 bits handles also.
 * Usage is simple. Call Register passing your unmanaged object handle, a destroy handle function, a free handle function (when
 * memory was allocated dynamically) and a list of parent objects represented with the same type of the main handle.
 *
 * When you are done using an object, typically on the Dispose() or Finalizer method, simply call Unregister() method, it will handle
 * decreasing refCount of parents and free what needs to be freed.
 *
 * In case you have dependencies that are found "lazily" later on the lifecycle of the object, you can can call AddDependency()
 * to add the new dependency discovered.
 *
 * If a dependency goes away, call RemoveDependency()
 *
 * Remarks: This library is entirely thread-safe not imposing serialization on the callers at all. Only assumption is that caller
 *          uses the library properly on its calls to Unregister() through explicit calls to Dispose() or usage of using() clause
 *
 *          Circular dependencies are not detected. If you have circular dependencies, until at least one dependency if broken
 *          you will be causing a memory leak. This is no different than with other reference counted strategies.
 */

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace GChelpers
{
  public class UnmanagedObjectLifecycle<THandleType>
  {
    private class ClassNameHandlePair : Tuple<string, THandleType>
    {
      public ClassNameHandlePair(string className, THandleType handle) : base(className, handle) { }
    }

    private class TrackedObjects : ConcurrentDictionary<Tuple<string, THandleType>, UnmanagedObjectContext<THandleType>> { }

    private readonly TrackedObjects _trackedObjects = new TrackedObjects();

    public void Register(string typeName, THandleType obj,
                         UnmanagedObjectContext<THandleType>.DestroyOrFreeUnmanagedObjectDelegate destroyMethod = null,
                         UnmanagedObjectContext<THandleType>.DestroyOrFreeUnmanagedObjectDelegate freeMethod = null,
                         ConcurrentDependencies<THandleType> dependencies = null)
    {
      var typeNameObjTuple = new ClassNameHandlePair(typeName, obj);
      var trackedObject = new UnmanagedObjectContext<THandleType>
      {
        DestroyObj = destroyMethod,
        FreeObject = freeMethod,
        Dependencies = dependencies ?? new ConcurrentDependencies<THandleType>()
      };
      do
      {
        if (_trackedObjects.TryAdd(typeNameObjTuple, trackedObject))
        {
          foreach (var dep in trackedObject.Dependencies)
          {
            UnmanagedObjectContext<THandleType> depContext;
            if (!_trackedObjects.TryGetValue(dep, out depContext))
              throw new EObjectNotFound<THandleType>(dep.Item1, dep.Item2);
            depContext.AddRefCount();
          }
          return;
        }
        UnmanagedObjectContext<THandleType> existingContextObj;
        if (!_trackedObjects.TryGetValue(typeNameObjTuple, out existingContextObj))
          continue; /* Object just dropped and removed from another thread. Let's try again */
        /* If object already existed, under normal conditions AddRefCount() must return a value > 1.
         * If it returns <= 1 it means it just got decremented in another thread, reached zero and
         * it's about to be destroyed. So we will have to wait for that to happen and try again our
         * entire operation */
        if (existingContextObj.AddRefCount() <= 1)
        {
          /* Object is getting removed in another thread. Let's spin while we wait for it to be gone
           * from our _trackedObjects container */
          while (_trackedObjects.TryGetValue(typeNameObjTuple, out existingContextObj))
            Thread.Yield();
          continue;
        }
        trackedObject = existingContextObj;
        /* Object already exists, could be an stale object not yet garbage collected,
         * so we will set the new cleanup methods in place of the current ones */
        trackedObject.DestroyObj = destroyMethod;
        trackedObject.FreeObject = freeMethod;
        break;
      } while (true);
      foreach (var dep in trackedObject.Dependencies)
        AddDependency(trackedObject, dep);
    }

    public void Unregister(string typeName, THandleType obj)
    {
      var objTuple = new ClassNameHandlePair(typeName, obj);
      UnmanagedObjectContext<THandleType> objContext;
      if (!_trackedObjects.TryGetValue(objTuple, out objContext))
        throw new EObjectNotFound<THandleType>(objTuple.Item1, objTuple.Item2);
      if (objContext.ReleaseRefCount() > 0)
        return; // Object still alive
      if (!_trackedObjects.TryRemove(objTuple, out objContext))
        throw new EFailedObjectRemoval<THandleType>(objTuple.Item1, objTuple.Item2);
      objContext.DestroyAndFree(obj);
      foreach (var dep in objContext.Dependencies)
        Unregister(dep.Item1, dep.Item2);
    }

    private void GetObjectsContexts(ClassNameHandlePair obj1,
                                    ClassNameHandlePair obj2,
                                    out UnmanagedObjectContext<THandleType> context1,
                                    out UnmanagedObjectContext<THandleType> context2)
    {
      if (!_trackedObjects.TryGetValue(obj1, out context1))
        throw new EObjectNotFound<THandleType>(obj1.Item1, obj1.Item2);
      if (!_trackedObjects.TryGetValue(obj2, out context2))
        throw new EObjectNotFound<THandleType>(obj2.Item1, obj2.Item2);
    }

    private void AddDependency(UnmanagedObjectContext<THandleType> trackedObjectContext,
                               Tuple<string, THandleType> dep)
    {
      UnmanagedObjectContext<THandleType> depContext;
      if (!_trackedObjects.TryGetValue(dep, out depContext))
        throw new EObjectNotFound<THandleType>(dep.Item1, dep.Item2);
      if (trackedObjectContext.Dependencies.Add(dep.Item1, dep.Item2))
        depContext.AddRefCount();
    }

    public void AddDependency(string typeName, THandleType obj, string depTypeName, THandleType dep)
    {
      var objTuple = new ClassNameHandlePair(typeName, obj);
      UnmanagedObjectContext<THandleType> objContext;
      if (!_trackedObjects.TryGetValue(objTuple, out objContext))
        throw new EObjectNotFound<THandleType>(typeName, obj);
      AddDependency(objContext, new ClassNameHandlePair(depTypeName, dep));
    }

    public void RemoveDependecy(string typeName, THandleType obj, string depTypeName, THandleType dep)
    {
      var objTuple = new ClassNameHandlePair(typeName, obj);
      var depTuple = new ClassNameHandlePair(depTypeName, dep);
      UnmanagedObjectContext<THandleType> objContext;
      UnmanagedObjectContext<THandleType> depContext;
      GetObjectsContexts(objTuple, depTuple, out objContext, out depContext);
      if (!objContext.Dependencies.Remove(depTuple.Item1, depTuple.Item2))
        throw new EDependencyNotFound<THandleType>(depTypeName, dep);
      Unregister(depTypeName, dep);
    }
  }
}