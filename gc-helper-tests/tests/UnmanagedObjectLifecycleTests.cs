using System;
using System.Threading;
using NUnit.Framework;
using JSB.GChelpers;

namespace gc_helper_tests
{
  public class TesterClass : IDisposable
  {
    public static readonly UnmanagedObjectLifecycle<IntPtr> UnmanagedObjectLifecycle = new UnmanagedObjectLifecycle<IntPtr>();
    public bool destroyed;
    public IntPtr destroyedHandle;
    private static IntPtr _nextHandle = IntPtr.Zero;
    public IntPtr Handle { get; set; }

    public void DestroyObject(IntPtr obj)
    {
      destroyed = true;
      destroyedHandle = obj;
    }

    public TesterClass(IntPtr[] deps)
    {
      _nextHandle = IntPtr.Add(_nextHandle, 1);
      Handle = _nextHandle;
      var _deps = new ConcurrentDependencies<IntPtr>();
      foreach (var dep in deps)
      {
        _deps.Add(dep);
      }
      UnmanagedObjectLifecycle.Register(Handle, DestroyObject, null, _deps);
    }

    ~TesterClass()
    {
      Dispose(false);
    }

    public void Dispose(bool disposing)
    {
      UnmanagedObjectLifecycle.Unregister(Handle, disposing);
    }

    public void Dispose()
    {
      Dispose(true);
    }
  }

  public class UnamangedObjectLifecycleTests
  {
    [Test]
    public void BasicTest_Success()
    {
      var obj = new TesterClass(new IntPtr[] {});
      Assert.IsFalse(obj.destroyed);
      obj.Dispose();
      Assert.IsTrue(obj.destroyed);
      Assert.AreEqual(obj.Handle, obj.destroyedHandle);
    }

    [Test]
    public void SimpleDependencyDisposeLeafLast_Success()
    {
      var obj = new TesterClass(new IntPtr[] {});
      var obj2 = new TesterClass(new[] { obj.Handle });
      obj.Dispose();
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      obj2.Dispose();
      Assert.IsTrue(obj.destroyed);
      Assert.IsTrue(obj2.destroyed);
      Assert.AreEqual(obj.Handle, obj.destroyedHandle);
      Assert.AreEqual(obj2.Handle, obj2.destroyedHandle);
    }

    [Test]
    [ExpectedException(typeof(EObjectNotFound<IntPtr>))]
    public void NonExistingDependency_Fails()
    {
      // ReSharper disable once ObjectCreationAsStatement
      new TesterClass(new[] { IntPtr.Zero });
    }

    [Test]
    [ExpectedException(typeof(EObjectNotFound<IntPtr>))]
    public void UnregisterNonExistingObject_Fails()
    {
      // ReSharper disable once ObjectCreationAsStatement
      var obj = new TesterClass(new IntPtr[] {});
      obj.Handle = IntPtr.Add(obj.Handle, 1);
      TesterClass.UnmanagedObjectLifecycle.Unregister(obj.Handle, true);
    }

    [Test]
    public void MutipleDependenciesDisposeLeafLast_Success()
    {
      var obj = new TesterClass(new IntPtr[] { });
      var obj2 = new TesterClass(new IntPtr[] { });
      var obj3 = new TesterClass(new [] { obj.Handle, obj2.Handle});
      obj.Dispose();
      obj2.Dispose();
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      obj3.Dispose();
      Assert.IsTrue(obj.destroyed);
      Assert.IsTrue(obj2.destroyed);
      Assert.IsTrue(obj3.destroyed);
      Assert.AreEqual(obj.Handle, obj.destroyedHandle);
      Assert.AreEqual(obj2.Handle, obj2.destroyedHandle);
      Assert.AreEqual(obj3.Handle, obj3.destroyedHandle);
    }

    [Test]
    public void LinearHierachyOfDependenciesDisposeLeafLast_Success()
    {
      var obj = new TesterClass(new IntPtr[] { });
      var obj2 = new TesterClass(new [] { obj.Handle });
      var obj3 = new TesterClass(new[] { obj2.Handle });
      obj.Dispose();
      obj2.Dispose();
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      obj3.Dispose();
      Assert.IsTrue(obj.destroyed);
      Assert.IsTrue(obj2.destroyed);
      Assert.IsTrue(obj3.destroyed);
      Assert.AreEqual(obj.Handle, obj.destroyedHandle);
      Assert.AreEqual(obj2.Handle, obj2.destroyedHandle);
      Assert.AreEqual(obj3.Handle, obj3.destroyedHandle);
    }

    [Test]
    public void ComplexHierachyOfDependenciesDisposeLeafLast_Success()
    {
      var obj = new TesterClass(new IntPtr[] { });
      var obj2 = new TesterClass(new[] { obj.Handle });
      var obj3 = new TesterClass(new[] { obj.Handle });
      var obj4 = new TesterClass(new[] { obj3.Handle, obj2.Handle });
      obj.Dispose();
      obj2.Dispose();
      obj3.Dispose();
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      Assert.IsFalse(obj4.destroyed);
      obj4.Dispose();
      Assert.IsTrue(obj.destroyed);
      Assert.IsTrue(obj2.destroyed);
      Assert.IsTrue(obj3.destroyed);
      Assert.IsTrue(obj4.destroyed);
      Assert.AreEqual(obj.Handle, obj.destroyedHandle);
      Assert.AreEqual(obj2.Handle, obj2.destroyedHandle);
      Assert.AreEqual(obj3.Handle, obj3.destroyedHandle);
      Assert.AreEqual(obj4.Handle, obj4.destroyedHandle);
    }

    [Test]
    public void ComplexHierachyOfDependenciesEmulateGCRandomDisposalOrder_Success()
    {
      var obj = new TesterClass(new IntPtr[] { });
      var obj2 = new TesterClass(new[] { obj.Handle });
      var obj3 = new TesterClass(new[] { obj.Handle });
      var obj4 = new TesterClass(new[] { obj3.Handle, obj2.Handle });
      obj4.Dispose();
      Assert.IsTrue(obj4.destroyed);
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      obj2.Dispose();
      Assert.IsTrue(obj2.destroyed);
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj3.destroyed);
      obj3.Dispose();
      Assert.IsFalse(obj.destroyed);
      Assert.IsTrue(obj3.destroyed);
      obj.Dispose();
      Assert.IsTrue(obj.destroyed);
      Assert.AreEqual(obj.Handle, obj.destroyedHandle);
      Assert.AreEqual(obj2.Handle, obj2.destroyedHandle);
      Assert.AreEqual(obj3.Handle, obj3.destroyedHandle);
      Assert.AreEqual(obj4.Handle, obj4.destroyedHandle);
    }

    [Test]
    public void SimpleHierachyThreadedTestPerformance_Success()
    {
      var startTicks = Environment.TickCount;
      var objs1 = new TesterClass[1000];
      for (var i = 0; i < 1000; i++)
        objs1[i] = new TesterClass(new IntPtr[] {});
      var objs2 = new TesterClass[1000];
      for (var i = 0; i < 1000; i++)
        objs2[i] = new TesterClass(new [] { objs1[i].Handle });
      var thread1 = new Thread(() =>
      {
        foreach (var o in objs1)
          o.Dispose();
      });
      var thread2 = new Thread(() =>
      {
        foreach (var o in objs2)
          o.Dispose();
      });
      thread1.Start();
      thread2.Start();
      thread1.Join();
      thread2.Join();
      foreach (var o in objs1)
      {
        Assert.IsTrue(o.destroyed);
        Assert.AreEqual(o.destroyedHandle, o.Handle);
      }
      foreach (var o in objs2)
      {
        Assert.IsTrue(o.destroyed);
        Assert.AreEqual(o.destroyedHandle, o.Handle);
      }
      Assert.Less(Math.Abs(Environment.TickCount - startTicks), 100);
    }

    [Test]
    public void CircularDependenciesDisposeAll_Success()
    {
      var obj = new TesterClass(new IntPtr[] { });
      var obj2 = new TesterClass(new[] { obj.Handle });
      var obj3 = new TesterClass(new[] { obj2.Handle });
      TesterClass.UnmanagedObjectLifecycle.AddDependency(obj.Handle, obj3.Handle); // Circular dependency
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      obj.Dispose();
      obj2.Dispose();
      obj3.Dispose();
      Assert.IsTrue(obj.destroyed);
      Assert.IsTrue(obj2.destroyed);
      Assert.IsTrue(obj3.destroyed);
      Assert.AreEqual(obj.Handle, obj.destroyedHandle);
      Assert.AreEqual(obj2.Handle, obj2.destroyedHandle);
      Assert.AreEqual(obj3.Handle, obj3.destroyedHandle);
    }

    [Test]
    public void MultiCircularDependenciesDisposeAll_Success()
    {
      var obj = new TesterClass(new IntPtr[] { });
      var obj2 = new TesterClass(new[] { obj.Handle });
      var obj3 = new TesterClass(new[] { obj2.Handle, obj.Handle });
      TesterClass.UnmanagedObjectLifecycle.AddDependency(obj.Handle, obj3.Handle); // Circular dependency
      TesterClass.UnmanagedObjectLifecycle.AddDependency(obj.Handle, obj2.Handle); // Circular dependency
      TesterClass.UnmanagedObjectLifecycle.AddDependency(obj2.Handle, obj3.Handle); // Circular dependency
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      obj.Dispose();
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      obj2.Dispose();
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      obj3.Dispose();
      Assert.IsTrue(obj.destroyed);
      Assert.IsTrue(obj2.destroyed);
      Assert.IsTrue(obj3.destroyed);
      Assert.AreEqual(obj.Handle, obj.destroyedHandle);
      Assert.AreEqual(obj2.Handle, obj2.destroyedHandle);
      Assert.AreEqual(obj3.Handle, obj3.destroyedHandle);
    }
  }
}
