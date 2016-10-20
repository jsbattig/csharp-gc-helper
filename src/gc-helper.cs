using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace JSB.GChelpers
{
  public class DisposeHelper
  {
    private static readonly ConcurrentDictionary<IntPtr, UnmanagedObjectContext> _trackedObjects = new ConcurrentDictionary<IntPtr, UnmanagedObjectContext>(); 
    public static void Register(IntPtr obj, 
                         UnmanagedObjectContext.DestroyOrFreeUnmanagedObjectDelegate destroyMethod, 
                         UnmanagedObjectContext.DestroyOrFreeUnmanagedObjectDelegate freeMethod,
                         List<IntPtr> dependencies)
    {
      _trackedObjects.TryAdd(obj, new UnmanagedObjectContext
      {
        Obj = obj,
        DestroyObj = destroyMethod,
        FreeObject = freeMethod,
        RefCount = 1,
        Dependencies = dependencies
      });
      if (dependencies == null)
        return;
      foreach (var dep in dependencies)
      {
        UnmanagedObjectContext depContext;
        if(_trackedObjects.TryGetValue(dep, out depContext))
          depContext.AddRefCount();
      }
    }

    public static void Unregister(IntPtr obj)
    {
      UnmanagedObjectContext depContext;
      if (!_trackedObjects.TryGetValue(obj, out depContext))
        return;
      if (depContext.ReleaseRefCount())
        _trackedObjects.TryRemove(obj, out depContext);
      if (depContext.Dependencies == null)
        return;
      foreach (var dep in depContext.Dependencies)
      {
        UnmanagedObjectContext depsDepContext;
        if (_trackedObjects.TryGetValue(dep, out depsDepContext) && depsDepContext.ReleaseRefCount())
          Unregister(dep);
      }
    }
  }
}