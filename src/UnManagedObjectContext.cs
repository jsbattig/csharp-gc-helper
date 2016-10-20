using System.Threading;

namespace JSB.GChelpers
{
  public class UnmanagedObjectContext<THandleType>
  {
    public delegate void DestroyOrFreeUnmanagedObjectDelegate(THandleType obj);

    public THandleType Obj { get; set; }
    public DestroyOrFreeUnmanagedObjectDelegate DestroyObj { get; set; }
    public DestroyOrFreeUnmanagedObjectDelegate FreeObject { get; set; }
    private int _refCount = 1;

    public ConcurrentDependencies<THandleType> Dependencies { get; set; }

    private void DestroyAndFree(THandleType obj)
    {
      DestroyObj?.Invoke(obj);
      FreeObject?.Invoke(obj);
    }

    public void AddRefCount()
    {
      Interlocked.Increment(ref _refCount);
    }

    public bool ReleaseRefCount()
    {
      // This can go to -1 when object was detroyed while destroying a dependent object
      if (Interlocked.Decrement(ref _refCount) > 0)
        return false;
      if(_refCount == 0)
        DestroyAndFree(Obj);
      return true;
    }
  }
}