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
 */

using System.Collections.Concurrent;

namespace JSB.GChelpers
{
  public class UnmanagedObjectLifecycle<THandleType>
  {
    private readonly ConcurrentDictionary<THandleType, UnmanagedObjectContext<THandleType>> _trackedObjects = new ConcurrentDictionary<THandleType, UnmanagedObjectContext<THandleType>>(); 
    public void Register(THandleType obj, UnmanagedObjectContext<THandleType>.DestroyOrFreeUnmanagedObjectDelegate destroyMethod,
                         UnmanagedObjectContext<THandleType>.DestroyOrFreeUnmanagedObjectDelegate freeMethod, ConcurrentDependencies<THandleType> dependencies)
    {
      if (dependencies == null)
        dependencies = new ConcurrentDependencies<THandleType>();
      _trackedObjects.TryAdd(obj, new UnmanagedObjectContext<THandleType>
      {
        Obj = obj,
        DestroyObj = destroyMethod,
        FreeObject = freeMethod,
        Dependencies = dependencies
      });
      foreach (var dep in dependencies)
      {
        UnmanagedObjectContext<THandleType> depContext;
        if(_trackedObjects.TryGetValue(dep, out depContext))
          depContext.AddRefCount();
      }
    }

    public void Unregister(THandleType obj, bool disposing)
    {
      UnmanagedObjectContext<THandleType> depContext;
      if (!_trackedObjects.TryGetValue(obj, out depContext))
        if (disposing)
          throw new EDisposeHelperObjectNotFound();
        else return;
      if (!depContext.ReleaseRefCount())
        return;
      _trackedObjects.TryRemove(obj, out depContext);
      foreach (var dep in depContext.Dependencies)
      {
        UnmanagedObjectContext<THandleType> depsDepContext;
        if (!_trackedObjects.TryGetValue(dep, out depsDepContext))
          if (disposing)
            throw new EDependencyNotFound();
          else continue;
        if (depsDepContext.ReleaseRefCount())
          Unregister(dep, disposing);
      }
    }

    public void AddDependency(THandleType obj, THandleType dep)
    {
      UnmanagedObjectContext<THandleType> depContext;
      if (!_trackedObjects.TryGetValue(obj, out depContext))
        throw new EDisposeHelperObjectNotFound();
      depContext.Dependencies.Add(dep);
    }

    public void RemoveDependecy(THandleType obj, THandleType dep)
    {
      UnmanagedObjectContext<THandleType> depContext;
      if (!_trackedObjects.TryGetValue(obj, out depContext))
        throw new EDisposeHelperObjectNotFound();
      depContext.Dependencies.Remove(dep);
    }
  }
}