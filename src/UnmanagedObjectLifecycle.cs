/*
 * This module allows developers to "register" with a given scope unmanaged objects represented by <templated> type.
 * Typically this type will be IntPtr, but will work well if underlying library uses 32 or 64 bits handles.
 * Usage is simple. Call Register passing your unmanaged object handle, a destroy function a free function (when 
 * memory was allocated dynamically) and a list of parent objects represented with the same type of the main handle.
 * 
 * When you are done using an object, typically on the Dispose() method, simply call Unregister() method, it will handle
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
      var trackedObject = new UnmanagedObjectContext<THandleType>
      {
        Obj = obj,
        DestroyObj = destroyMethod,
        FreeObject = freeMethod,
        Dependencies = dependencies ?? new ConcurrentDependencies<THandleType>()
      };
      foreach (var dep in trackedObject.Dependencies)
      {
        UnmanagedObjectContext<THandleType> depContext;
        if(!_trackedObjects.TryGetValue(dep, out depContext))
          throw new EObjectNotFound<THandleType>(dep);
        depContext.AddRefCount();
      }
      if (!_trackedObjects.TryAdd(obj, trackedObject))
        trackedObject.AddRefCount();
        //throw new EObjectAlreadyExists<THandleType>(obj);
    }

    /* currentlyUnregistering parameter is used to allow circular dependency relationship. When found on a recursive 
       situation unregistering related objects in a circular relationship the code will cut control properly when reaching the
       starting point of the recursive stack */
    private void Unregister(THandleType obj, bool disposing, Dependencies<THandleType> currentlyUnregistering)
    {
      UnmanagedObjectContext<THandleType> objContext;
      if (!_trackedObjects.TryGetValue(obj, out objContext))
        if (disposing)
          throw new EObjectNotFound<THandleType>(obj);
        else return;
      if (!objContext.ReleaseRefCount())
        return;
      if (!_trackedObjects.TryRemove(obj, out objContext))
        throw new EObjectNotFound<THandleType>(obj);
      foreach (var dep in objContext.Dependencies)
      {
        UnmanagedObjectContext<THandleType> depsDepContext;
        if (!_trackedObjects.TryGetValue(dep, out depsDepContext))
          if (disposing && (currentlyUnregistering == null || currentlyUnregistering.Find(dep)))
            throw new EDependencyObjectNotFound<THandleType>(dep);
          else continue;
        if (!depsDepContext.ReleaseRefCount())
          continue;
        currentlyUnregistering = currentlyUnregistering ?? new Dependencies<THandleType>();
        currentlyUnregistering.Add(obj);
        try
        {
          Unregister(dep, disposing, currentlyUnregistering);
        }
        finally
        {
          currentlyUnregistering.Remove(obj);
        }
      }
    }

    public void Unregister(THandleType obj, bool disposing)
    {
      Unregister(obj, disposing, null);
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

    public void AddDependency(THandleType obj, THandleType dep)
    {
      UnmanagedObjectContext<THandleType> objContext;
      UnmanagedObjectContext<THandleType> depContext;
      GetObjectsContexts(obj, dep, out objContext, out depContext);
      objContext.Dependencies.Add(dep);
      depContext.AddRefCount();
    }

    public void RemoveDependecy(THandleType obj, THandleType dep)
    {
      UnmanagedObjectContext<THandleType> objContext;
      UnmanagedObjectContext<THandleType> depContext;
      GetObjectsContexts(obj, dep, out objContext, out depContext);
      if (!objContext.Dependencies.Remove(dep))
        throw new EDependencyNotFound<THandleType>(dep);
      if (depContext.ReleaseRefCount())
        Unregister(dep, true);
    }
  }
}