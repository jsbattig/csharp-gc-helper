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

using System.Collections.Concurrent;

namespace GChelpers
{
  public class UnmanagedObjectLifecycle<THandleType>
  {
    private readonly ConcurrentDictionary<THandleType, UnmanagedObjectContext<THandleType>> _trackedObjects = new ConcurrentDictionary<THandleType, UnmanagedObjectContext<THandleType>>();

    public void Register(THandleType obj,
                         UnmanagedObjectContext<THandleType>.DestroyOrFreeUnmanagedObjectDelegate destroyMethod = null,
                         UnmanagedObjectContext<THandleType>.DestroyOrFreeUnmanagedObjectDelegate freeMethod = null,
                         ConcurrentDependencies<THandleType> dependencies = null)
    {
      UnmanagedObjectContext<THandleType> trackedObject;
      do
      {
        trackedObject = new UnmanagedObjectContext<THandleType>
        {
          Obj = obj,
          DestroyObj = destroyMethod,
          FreeObject = freeMethod,
          Dependencies = dependencies = dependencies ?? new ConcurrentDependencies<THandleType>()
        };
        if (_trackedObjects.TryAdd(obj, trackedObject))
        {
          foreach (var dep in trackedObject.Dependencies)
          {
            UnmanagedObjectContext<THandleType> depContext;
            if (!_trackedObjects.TryGetValue(dep, out depContext))
              throw new EObjectNotFound<THandleType>(dep);
            depContext.AddRefCount();
          }
          return;
        }
        /* First attempt to get trackedObject */
        if (!_trackedObjects.TryGetValue(obj, out trackedObject))
          /* Same handle was being utilized by other object getting freed
           * at the same time we are reusing the handle. Try again */
          continue;
        trackedObject.AddRefCount();
        /* We need to try again to get our trackedObject after adding RefCount,
         * it's possible that another thread got rid of it anyway. Only guarantee is getting it again
         * after addingRefCount */
        if (!_trackedObjects.TryGetValue(obj, out trackedObject))
          /* Same handle was being utilized by other object getting freed
           * at the same time we are reusing the handle. Try again */
          continue;
        break;
      } while (true);
      foreach (var dep in dependencies)
        AddDependency(trackedObject, dep);
    }

    public void Unregister(THandleType obj)
    {
      UnmanagedObjectContext<THandleType> objContext;
      if (!_trackedObjects.TryGetValue(obj, out objContext))
        throw new EObjectNotFound<THandleType>(obj);
      //return; // Object was removed in another thread reaching refcount <= 0?
      if (!objContext.ReleaseRefCount())
        return; // Object still alive
      if (!_trackedObjects.TryRemove(obj, out objContext))
        throw new EFailedObjectRemoval<THandleType>(obj);
      //return; // Object was removed in another thread reaching refcount <= 0?
      if (objContext.RefCount > 0)
        _trackedObjects.TryAdd(obj, objContext); // Need to re-add object, same object added with a call to Register()
      else foreach (var dep in objContext.PriorDependencies)
        Unregister(dep);
    }

    private void GetObjectsContexts(THandleType obj1, THandleType obj2,
                                    out UnmanagedObjectContext<THandleType> context1,
                                    out UnmanagedObjectContext<THandleType> context2)
    {
      if (!_trackedObjects.TryGetValue(obj1, out context1))
        throw new EObjectNotFound<THandleType>(obj1);
      if (!_trackedObjects.TryGetValue(obj2, out context2))
        throw new EObjectNotFound<THandleType>(obj2);
    }

    private void AddDependency(UnmanagedObjectContext<THandleType> trackedObjectContext, THandleType dep)
    {
      UnmanagedObjectContext<THandleType> depContext;
      if (!_trackedObjects.TryGetValue(dep, out depContext))
        throw new EObjectNotFound<THandleType>(dep);
      trackedObjectContext.LockDependencies();
      try
      {
        if (trackedObjectContext.Dependencies.Add(dep))
          depContext.AddRefCount();
      }
      finally
      {
        trackedObjectContext.UnlockDependencies();
      }
    }

    public void AddDependency(THandleType obj, THandleType dep)
    {
      UnmanagedObjectContext<THandleType> objContext;
      if (!_trackedObjects.TryGetValue(obj, out objContext))
        throw new EObjectNotFound<THandleType>(obj);
      AddDependency(objContext, dep);
    }

    public void RemoveDependecy(THandleType obj, THandleType dep)
    {
      UnmanagedObjectContext<THandleType> objContext;
      UnmanagedObjectContext<THandleType> depContext;
      GetObjectsContexts(obj, dep, out objContext, out depContext);
      objContext.LockDependencies();
      try
      {
        if (!objContext.Dependencies.Remove(dep))
          throw new EDependencyNotFound<THandleType>(dep);
      }
      finally
      {
        objContext.UnlockDependencies();
      }
      Unregister(dep);
    }
  }
}