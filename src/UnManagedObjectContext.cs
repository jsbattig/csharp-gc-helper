using System;
using System.Collections.Generic;
using System.Threading;

namespace JSB.GChelpers
{
  public class UnmanagedObjectContext
  {
    public delegate void DestroyOrFreeUnmanagedObjectDelegate(IntPtr obj);

    public IntPtr Obj { get; set; }
    public DestroyOrFreeUnmanagedObjectDelegate DestroyObj { get; set; }
    public DestroyOrFreeUnmanagedObjectDelegate FreeObject { get; set; }
    private int _refCount;

    public int RefCount
    {
      get
      {
        return _refCount;
      }
      set
      {
        _refCount = value;
      }
    }
    public List<IntPtr> Dependencies { get; set; }

    private void DestroyAndFree(IntPtr obj)
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
      if (Interlocked.Decrement(ref _refCount) > 0)
        return false;
      DestroyAndFree(Obj);
      return true;
    }
  }
}