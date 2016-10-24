using System.Threading;

namespace GChelpers
{
  public class UnmanagedObjectContext<THandleType>
  {
    public delegate void DestroyOrFreeUnmanagedObjectDelegate(THandleType obj);

    private int _refCount = 1;
    public DestroyOrFreeUnmanagedObjectDelegate DestroyObj { get; set; }
    public DestroyOrFreeUnmanagedObjectDelegate FreeObject { get; set; }
    public ConcurrentDependencies<THandleType> Dependencies { get; set; }

    public void DestroyAndFree(THandleType obj)
    {
      if (DestroyObj != null)
        DestroyObj.Invoke(obj);
      if (FreeObject != null)
        FreeObject.Invoke(obj);
    }

    public int AddRefCount()
    {
      return Interlocked.Increment(ref _refCount);
    }

    public int ReleaseRefCount()
    {
      // This can go to -1 when object was detroyed while destroying a dependent object
      var localRefCount = Interlocked.Decrement(ref _refCount);
      if (localRefCount < 0)
        throw new EGChelper("RefCount can't be lower than zero");
      return localRefCount;
    }
  }
}