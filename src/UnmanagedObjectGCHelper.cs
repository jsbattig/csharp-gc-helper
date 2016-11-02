using System;
using System.Collections.Concurrent;
using System.Threading;

namespace GChelpers
{
  internal interface IHandleRemover<in THandleClass, in THandle>
  {
    void RemoveAndDestroyHandle(THandleClass handleClass, THandle obj);
  }

  /// <summary>
  ///
  ///  This class helps to track unmanaged handles, typically coming from an unmanaged DLL.
  ///  You may want to use this class in situations where the handles have intrinsict relationships
  ///  between them dictating that they can't be left to the garbage collector to be disposed thorough
  ///  wrapper object finalizer.
  ///
  ///  With this class a tracking record can be established per handle that needs to be tracked, specifying
  ///  a object destructor method and a object free method. The destruction may not deallocate the
  ///  unmanaged memory, while the free method will do that.
  ///
  /// </summary>
  // ReSharper disable once InconsistentNaming
  public class UnmanagedObjectGCHelper<THandleClass, THandle> : IHandleRemover<THandleClass, THandle>, IDisposable
  {
    public class HandleContainer : Tuple<THandleClass, THandle>
    {
      public HandleContainer(THandleClass handleClass, THandle handle) : base(handleClass, handle) { }
    }

    public delegate void ExceptionDelegate(UnmanagedObjectGCHelper<THandleClass, THandle> obj, Exception exception, THandleClass handleClass, THandle handle);
    private class TrackedObjects : ConcurrentDictionary<Tuple<THandleClass, THandle>, UnmanagedObjectContext<THandleClass, THandle>> { }
    private bool _agentRunning;
    private readonly TrackedObjects _trackedObjects = new TrackedObjects();
    private readonly UnregistrationAgent<THandleClass, THandle> _unregistrationAgent;

    public ExceptionDelegate OnUnregisterException { get; set; }

    public UnmanagedObjectGCHelper()
    {
      _unregistrationAgent = new UnregistrationAgent<THandleClass, THandle>(this);
      _agentRunning = true;
    }

    private void Dispose(bool disposing)
    {
      if (!disposing)
        return;
      _unregistrationAgent.Dispose();
    }

    public void Dispose()
    {
      Dispose(true);
    }

    public void StopAgent()
    {
      _agentRunning = false;
      _unregistrationAgent.Stop();
    }

    public void Register(THandleClass handleClass, THandle obj,
                         UnmanagedObjectContext<THandleClass, THandle>.DestroyHandleDelegate destroyHandle = null,
                         ConcurrentDependencies<THandleClass, THandle> dependencies = null)
    {
      var handleContainer = new HandleContainer(handleClass, obj);
      var trackedObject = new UnmanagedObjectContext<THandleClass, THandle>
      {
        DestroyHandle = destroyHandle,
        Dependencies = dependencies
      };
      do
      {
        if (_trackedObjects.TryAdd(handleContainer, trackedObject))
        {
          if (dependencies == null)
            return;
          foreach (var dep in trackedObject.Dependencies)
          {
            UnmanagedObjectContext<THandleClass, THandle> depContext;
            if (!_trackedObjects.TryGetValue(dep, out depContext))
              throw new EObjectNotFound<THandleClass, THandle>(dep.Item1, dep.Item2);
            depContext.AddRefCount();
          }
          return;
        }
        UnmanagedObjectContext<THandleClass, THandle> existingContextObj;
        if (!_trackedObjects.TryGetValue(handleContainer, out existingContextObj))
          continue; /* Object just dropped and removed from another thread. Let's try again */
        /* If object already existed, under normal conditions AddRefCount() must return a value > 1.
         * If it returns <= 1 it means it just got decremented in another thread, reached zero and
         * it's about to be destroyed. So we will have to wait for that to happen and try again our
         * entire operation */
        var newRefCount = existingContextObj.AddRefCount();
        if (newRefCount <= 0)
          throw new EInvalidRefCount<THandleClass, THandle>(handleClass, obj, newRefCount);
        if (newRefCount == 1)
        {
          /* Object is getting removed in another thread. Let's spin while we wait for it to be gone
           * from our _trackedObjects container */
          while (_trackedObjects.TryGetValue(handleContainer, out existingContextObj))
            Thread.Yield();
          continue;
        }
        trackedObject = existingContextObj;
        /* Object already exists, could be an stale object not yet garbage collected,
         * so we will set the new cleanup methods in place of the current ones */
        trackedObject.DestroyHandle = destroyHandle;
        break;
      } while (true);
      if (dependencies == null)
        return;
      foreach (var dep in dependencies)
        AddDependency(trackedObject, dep);
    }

    public void RemoveAndDestroyHandle(THandleClass handleClass, THandle obj)
    {
      try
      {
        var objTuple = new HandleContainer(handleClass, obj);
        UnmanagedObjectContext<THandleClass, THandle> objContext;
        if (!_trackedObjects.TryGetValue(objTuple, out objContext))
          throw new EObjectNotFound<THandleClass, THandle>(objTuple.Item1, objTuple.Item2);
        var newRefCount = objContext.ReleaseRefCount();
        if (newRefCount > 0)
          return; // Object still alive
        if (newRefCount < 0)
          throw new EInvalidRefCount<THandleClass, THandle>(handleClass, obj, newRefCount);
        try
        {
          objContext.DestroyAndFree(obj);
          if (objContext.Dependencies == null)
            return;
          foreach (var dep in objContext.Dependencies)
            RemoveDependency(handleClass, obj, objContext, dep);
        }
        finally
        {
          if (!_trackedObjects.TryRemove(objTuple, out objContext))
            throw new EFailedObjectRemoval<THandleClass, THandle>(objTuple.Item1, objTuple.Item2);
        }
      }
      catch (Exception e)
      {
        if (OnUnregisterException == null)
          return;
        OnUnregisterException(this, e, handleClass, obj);
      }
    }

    public void Unregister(THandleClass handleClass, THandle obj)
    {
      /* the following code as regards to _agentRunning is not thread safe. But I don't want we pay the cost of a lock operation here
       * not even a spinlock call. We want Unregister to be as fast as possible. Worst thing that can happen we may leak some unmanaged object
       * that gets inserted into _unregistrationAgent queue and never destroyed */
      if (_agentRunning)
        _unregistrationAgent.Enqueue(handleClass, obj);
      else
        RemoveAndDestroyHandle(handleClass, obj);
    }

    private void AddDependency(UnmanagedObjectContext<THandleClass, THandle> trackedObjectContext,
                               Tuple<THandleClass, THandle> dep)
    {
      UnmanagedObjectContext<THandleClass, THandle> depContext;
      if (!_trackedObjects.TryGetValue(dep, out depContext))
        throw new EObjectNotFound<THandleClass, THandle>(dep.Item1, dep.Item2);
      if(trackedObjectContext.Dependencies == null)
        trackedObjectContext.InitDependencies();
      if (trackedObjectContext.Dependencies.Add(dep.Item1, dep.Item2))
        depContext.AddRefCount();
    }

    private void RemoveDependency(THandleClass handleClass, THandle obj,
                                 UnmanagedObjectContext<THandleClass, THandle> trackedObjectContext,
                                 Tuple<THandleClass, THandle> dep)
    {

      if (trackedObjectContext.Dependencies == null ||
          !trackedObjectContext.Dependencies.Remove(dep.Item1, dep.Item2))
        throw new EDependencyNotFound<THandleClass, THandle>(dep.Item1, dep.Item2);
      Unregister(dep.Item1, dep.Item2);
    }

    public void AddDependency(THandleClass handleClass, THandle obj, THandleClass depHandleClass, THandle dep)
    {
      var objTuple = new HandleContainer(handleClass, obj);
      UnmanagedObjectContext<THandleClass, THandle> objContext;
      if (!_trackedObjects.TryGetValue(objTuple, out objContext))
        throw new EObjectNotFound<THandleClass, THandle>(handleClass, obj);
      AddDependency(objContext, new HandleContainer(depHandleClass, dep));
    }

    public void RemoveDependency(THandleClass handleClass, THandle obj, THandleClass depHandleClass, THandle dep)
    {
      var objTuple = new HandleContainer(handleClass, obj);
      UnmanagedObjectContext<THandleClass, THandle> objContext;
      if (!_trackedObjects.TryGetValue(objTuple, out objContext))
        throw new EObjectNotFound<THandleClass, THandle>(handleClass, obj);
      var depTuple = new HandleContainer(depHandleClass, dep);
      RemoveDependency(handleClass, obj, objContext, depTuple);
    }
  }
}