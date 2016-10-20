using System;
using System.Collections.Generic;
using NUnit.Framework;
using JSB.GChelpers;

namespace gc_helper_tests
{
  public class TesterClass : IDisposable
  {
    public bool destroyed;
    public IntPtr destroyedHandle;
    private static IntPtr _nextHandle = IntPtr.Zero;
    public IntPtr Handle { get; set; }

    public void DestroyObject(IntPtr obj)
    {
      destroyed = true;
      destroyedHandle = obj;
    }

    public TesterClass(IntPtr dep)
    {
      _nextHandle = IntPtr.Add(_nextHandle, 1);
      Handle = _nextHandle;
      List<IntPtr> deps;
      deps = dep != IntPtr.Zero ? new List<IntPtr> {dep} : null;
      DisposeHelper.Register(Handle, DestroyObject, null, deps);
    }

    ~TesterClass()
    {
      Dispose(false);
    }

    public void Dispose(bool disposing)
    {
      DisposeHelper.Unregister(Handle);
    }

    public void Dispose()
    {
      Dispose(true);
    }
  }

  public class GCHelperTests
  {
    [Test]
    public void BasicTest()
    {
      var obj = new TesterClass(IntPtr.Zero);
      Assert.IsFalse(obj.destroyed);
      obj.Dispose();
      Assert.IsTrue(obj.destroyed);
      Assert.AreEqual(obj.Handle, obj.destroyedHandle);
    }

    [Test]
    public void SimpleDependency()
    {
      var obj = new TesterClass(IntPtr.Zero);
      var obj2 = new TesterClass(obj.Handle);
      obj.Dispose();
      Assert.IsFalse(obj.destroyed);
      obj2.Dispose();
      Assert.IsTrue(obj.destroyed);
      Assert.IsTrue(obj2.destroyed);
      Assert.AreEqual(obj.Handle, obj.destroyedHandle);
      Assert.AreEqual(obj2.Handle, obj2.destroyedHandle);
    }
  }
}
