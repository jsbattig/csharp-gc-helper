using System.Threading;

namespace GChelpers
{
  public class UnmanagedObjectContext<THandleClass, THandle>
  {
    public delegate void DestroyOrFreeUnmanagedObjectDelegate(THandle obj);

    private int _refCount = 1;
    public DestroyOrFreeUnmanagedObjectDelegate DestroyObj { get; set; }
    public DestroyOrFreeUnmanagedObjectDelegate FreeObject { get; set; }
    public ConcurrentDependencies<THandleClass, THandle> Dependencies { get; set; }

    public void DestroyAndFree(THandle obj)
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