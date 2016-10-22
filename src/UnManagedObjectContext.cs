using System.Threading;

namespace GChelpers
{
  public class UnmanagedObjectContext<THandleType>
  {
    public delegate void DestroyOrFreeUnmanagedObjectDelegate(THandleType obj);

    private int _refCount = 1;
    private SpinLock _refCountLock = new SpinLock();
    public THandleType Obj { get; set; }
    public DestroyOrFreeUnmanagedObjectDelegate DestroyObj { get; set; }
    public DestroyOrFreeUnmanagedObjectDelegate FreeObject { get; set; }
    public ConcurrentDependencies<THandleType> Dependencies { get; set; }
    public ConcurrentDependencies<THandleType> PriorDependencies { get; private set; }

    public int RefCount
    {
      get
      {
        LockDependencies();
        try
        {
          return _refCount;
        }
        finally
        {
          UnlockDependencies();
        }
      }
    }

    private void DestroyAndFree(THandleType obj)
    {
      if (DestroyObj != null)
        DestroyObj.Invoke(obj);
      if (FreeObject != null)
        FreeObject.Invoke(obj);
    }

    public void LockDependencies()
    {
      var lockTaken = false;
      while (!lockTaken)
        _refCountLock.Enter(ref lockTaken);
    }

    public void UnlockDependencies()
    {
      _refCountLock.Exit();
    }

    public void AddRefCount()
    {
      LockDependencies();
      try
      {
        _refCount++;
      }
      finally
      {
        UnlockDependencies();
      }
    }

    public bool ReleaseRefCount()
    {
      int localRefCount;
      LockDependencies();
      try
      {
        // This can go to -1 when object was detroyed while destroying a dependent object
        localRefCount = --_refCount;
        if (localRefCount < 0)
          throw new EDisposeHelper("RefCount can't be lower than zero");
        if (localRefCount > 0)
          return false;
        else if (localRefCount == 0)
        {
          PriorDependencies = Dependencies;
          Dependencies = new ConcurrentDependencies<THandleType>();
        }
      }
      finally
      {
        UnlockDependencies();
      }
      if(localRefCount == 0)
        DestroyAndFree(Obj);
      return true;
    }
  }
}