using System.Threading;

namespace GChelpers
{
  public class UnmanagedObjectContext<THandleClass, THandle>
  {
    public delegate void DestroyHandleDelegate(THandle obj);

    private int _refCount = 1;
    public DestroyHandleDelegate DestroyHandle { get; set; }
    public ConcurrentDependencies<THandleClass, THandle> Dependencies { get; set; }

    public void DestroyAndFree(THandle obj)
    {
      if (DestroyHandle != null)
        DestroyHandle(obj);
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