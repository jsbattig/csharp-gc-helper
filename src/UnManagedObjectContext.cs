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
      return Interlocked.Decrement(ref _refCount);
    }
  }
}