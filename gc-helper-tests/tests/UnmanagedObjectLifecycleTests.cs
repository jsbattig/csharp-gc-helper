using System;
using System.Threading;
using NUnit.Framework;
using GChelpers;

namespace gc_helper_tests
{
  public class TesterClass
  {
    public static readonly UnmanagedObjectGCHelper<string, IntPtr> UnmanagedObjectLifecycle = new UnmanagedObjectGCHelper<string, IntPtr>();
    public bool destroyed;
    public IntPtr destroyedHandle;
    private static IntPtr _nextHandle = IntPtr.Zero;
    public IntPtr Handle { get; set; }

    public void DestroyObject(IntPtr obj)
    {
      destroyed = true;
      destroyedHandle = obj;
    }

    public UnmanagedObjectContext<string, IntPtr>.DestroyHandleDelegate GetDestroyDelegate()
    {
      return DestroyObject;
    }

    public TesterClass(IntPtr[] deps)
    {
      _nextHandle = IntPtr.Add(_nextHandle, 1);
      Handle = _nextHandle;
      var _deps = new ConcurrentDependencies<string, IntPtr>();
      foreach (var dep in deps)
      {
        _deps.Add(typeof(TesterClass).Name, dep);
      }
      UnmanagedObjectLifecycle.Register(typeof(TesterClass).Name, Handle, DestroyObject, _deps);
    }

    public void Dispose()
    {
      UnmanagedObjectLifecycle.Unregister(typeof(TesterClass).Name, Handle);
    }
  }

  public class UnamangedObjectLifecycleTests
  {
    private Exception _raisedException;
    public void ExceptionUnregisteringHandle(UnmanagedObjectGCHelper<string, IntPtr> obj, Exception exception,
      string typeName, IntPtr handle)
    {
      _raisedException = exception;
    }

    [SetUp]
    public void Setup()
    {
      _raisedException = null;
      TesterClass.UnmanagedObjectLifecycle.OnUnregisterException = ExceptionUnregisteringHandle;
    }

    [TearDown]
    public void Teardown()
    {
      TesterClass.UnmanagedObjectLifecycle.OnUnregisterException = null;
      Assert.IsNull(_raisedException);
    }

    [TestFixtureTearDown]
    public void TestFixtureTeardown()
    {
      TesterClass.UnmanagedObjectLifecycle.Dispose();
    }

    [Test]
    public void BasicTest_Success()
    {
      var obj = new TesterClass(new IntPtr[] {});
      Assert.IsFalse(obj.destroyed);
      obj.Dispose();
      Thread.Sleep(200);
      Assert.IsTrue(obj.destroyed);
      Assert.AreEqual(obj.Handle, obj.destroyedHandle);
    }

    [Test]
    public void BasicTestTwoDifferentTypeNames_Success()
    {
      var obj = new TesterClass(new IntPtr[] { });
      TesterClass.UnmanagedObjectLifecycle.Register("AnotherClass", obj.Handle);
      TesterClass.UnmanagedObjectLifecycle.Unregister("AnotherClass", obj.Handle);
      TesterClass.UnmanagedObjectLifecycle.Unregister("AnotherClass", obj.Handle);
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsTrue(_raisedException is EObjectNotFound<string, IntPtr>);
      _raisedException = null;
      obj.Dispose();
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsTrue(obj.destroyed);
      Assert.AreEqual(obj.Handle, obj.destroyedHandle);
    }

    [Test]
    public void RegisterObjectTwice_Success()
    {
      var obj = new TesterClass(new IntPtr[] { });
      Assert.IsFalse(obj.destroyed);
      TesterClass.UnmanagedObjectLifecycle.Register(typeof(TesterClass).Name, obj.Handle, obj.GetDestroyDelegate());
      Assert.IsFalse(obj.destroyed);
      obj.Dispose();
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsFalse(obj.destroyed);
      TesterClass.UnmanagedObjectLifecycle.Unregister(typeof(TesterClass).Name, obj.Handle);
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsTrue(obj.destroyed);
      Assert.AreEqual(obj.Handle, obj.destroyedHandle);
    }

    [Test]
    //[ExpectedException(typeof(EExistingTrackedObjectDiffers<IntPtr>))]
    public void RegisterObjectTwiceDifferenDestroyDelegate_Fails()
    {
      var obj = new TesterClass(new IntPtr[] { });
      Assert.IsFalse(obj.destroyed);
      TesterClass.UnmanagedObjectLifecycle.Register(typeof(TesterClass).Name, obj.Handle);
    }

    [Test]
    public void SimpleDependencyDisposeLeafLast_Success()
    {
      var obj = new TesterClass(new IntPtr[] {});
      var obj2 = new TesterClass(new[] { obj.Handle });
      obj.Dispose();
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      obj2.Dispose();
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsTrue(obj.destroyed);
      Assert.IsTrue(obj2.destroyed);
      Assert.AreEqual(obj.Handle, obj.destroyedHandle);
      Assert.AreEqual(obj2.Handle, obj2.destroyedHandle);
    }

    [Test]
    public void SimpleDependencyDisposeByRemovingDependency_Success()
    {
      var obj = new TesterClass(new IntPtr[] { });
      var obj2 = new TesterClass(new[] { obj.Handle });
      obj.Dispose();
      Thread.Sleep(200);
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      TesterClass.UnmanagedObjectLifecycle.RemoveDependency(typeof(TesterClass).Name, obj2.Handle, typeof(TesterClass).Name, obj.Handle);
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsTrue(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      obj2.Dispose();
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsTrue(obj2.destroyed);
      Assert.AreEqual(obj.Handle, obj.destroyedHandle);
      Assert.AreEqual(obj2.Handle, obj2.destroyedHandle);
    }

    [Test]
    [ExpectedException(typeof(EDependencyNotFound<string, IntPtr>))]
    public void RemoveNotExistingDependency_Fails()
    {
      var obj = new TesterClass(new IntPtr[] { });
      var obj2 = new TesterClass(new[] { obj.Handle });
      TesterClass.UnmanagedObjectLifecycle.RemoveDependency(typeof(TesterClass).Name, obj.Handle, typeof(TesterClass).Name, obj2.Handle);
    }

    [Test]
    [ExpectedException(typeof(EObjectNotFound<string, IntPtr>))]
    public void NonExistingDependency_Fails()
    {
      // ReSharper disable once ObjectCreationAsStatement
      new TesterClass(new[] { IntPtr.Zero });
    }

    [Test]
    public void UnregisterNonExistingObject_Success()
    {
      // ReSharper disable once ObjectCreationAsStatement
      var obj = new TesterClass(new IntPtr[] {});
      obj.Handle = IntPtr.Add(obj.Handle, 1);
      TesterClass.UnmanagedObjectLifecycle.Unregister(typeof(TesterClass).Name, obj.Handle);
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsTrue(_raisedException is EObjectNotFound<string, IntPtr>);
      _raisedException = null;
    }

    [Test]
    public void MutipleDependenciesDisposeLeafLast_Success()
    {
      var obj = new TesterClass(new IntPtr[] { });
      var obj2 = new TesterClass(new IntPtr[] { });
      var obj3 = new TesterClass(new [] { obj.Handle, obj2.Handle});
      obj.Dispose();
      obj2.Dispose();
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      obj3.Dispose();
      Thread.Yield();
      Thread.Sleep(200);
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
      Thread.Sleep(200);
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      obj3.Dispose();
      Thread.Yield();
      Thread.Sleep(200);
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
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      Assert.IsFalse(obj4.destroyed);
      obj4.Dispose();
      Thread.Yield();
      Thread.Sleep(200);
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
      Thread.Sleep(200);
      Assert.IsTrue(obj4.destroyed);
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      obj2.Dispose();
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsTrue(obj2.destroyed);
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj3.destroyed);
      obj3.Dispose();
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsFalse(obj.destroyed);
      Assert.IsTrue(obj3.destroyed);
      obj.Dispose();
      Thread.Yield();
      Thread.Sleep(200);
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
      Thread.Yield();
      Thread.Sleep(200);
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
      Assert.Less(Math.Abs(Environment.TickCount - startTicks), 500);
    }

    [Test]
    public void CircularDependenciesDisposeAllRemoveOneDependency_Success()
    {
      var obj = new TesterClass(new IntPtr[] { });
      var obj2 = new TesterClass(new[] { obj.Handle });
      var obj3 = new TesterClass(new[] { obj2.Handle });
      TesterClass.UnmanagedObjectLifecycle.AddDependency(typeof(TesterClass).Name, obj.Handle, typeof(TesterClass).Name, obj3.Handle); // Circular dependency
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      obj.Dispose();
      obj2.Dispose();
      obj3.Dispose();
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      /* Only way to get objects destroyed is by removing one dependency to destroy the circle */
      TesterClass.UnmanagedObjectLifecycle.RemoveDependency(typeof(TesterClass).Name, obj.Handle, typeof(TesterClass).Name, obj3.Handle);
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsTrue(obj.destroyed);
      Assert.IsTrue(obj2.destroyed);
      Assert.IsTrue(obj3.destroyed);
      Assert.AreEqual(obj.Handle, obj.destroyedHandle);
      Assert.AreEqual(obj2.Handle, obj2.destroyedHandle);
      Assert.AreEqual(obj3.Handle, obj3.destroyedHandle);
    }

    [Test]
    public void MultiCircularDependenciesDisposeAllRemoveDependencies_Success()
    {
      var obj = new TesterClass(new IntPtr[] { });
      var obj2 = new TesterClass(new[] { obj.Handle });
      var obj3 = new TesterClass(new[] { obj2.Handle, obj.Handle });
      TesterClass.UnmanagedObjectLifecycle.AddDependency(typeof(TesterClass).Name, obj.Handle, typeof(TesterClass).Name, obj3.Handle); // Circular dependency
      TesterClass.UnmanagedObjectLifecycle.AddDependency(typeof(TesterClass).Name, obj.Handle, typeof(TesterClass).Name, obj2.Handle); // Circular dependency
      TesterClass.UnmanagedObjectLifecycle.AddDependency(typeof(TesterClass).Name, obj2.Handle, typeof(TesterClass).Name, obj3.Handle); // Circular dependency
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      obj.Dispose();
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      obj2.Dispose();
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      obj3.Dispose();
      Thread.Sleep(200);
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      TesterClass.UnmanagedObjectLifecycle.RemoveDependency(typeof(TesterClass).Name, obj.Handle, typeof(TesterClass).Name, obj3.Handle);
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      TesterClass.UnmanagedObjectLifecycle.RemoveDependency(typeof(TesterClass).Name, obj.Handle, typeof(TesterClass).Name, obj2.Handle);
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsFalse(obj.destroyed);
      Assert.IsFalse(obj2.destroyed);
      Assert.IsFalse(obj3.destroyed);
      TesterClass.UnmanagedObjectLifecycle.RemoveDependency(typeof(TesterClass).Name, obj2.Handle, typeof(TesterClass).Name, obj3.Handle);
      Thread.Yield();
      Thread.Sleep(200);
      Assert.IsTrue(obj.destroyed);
      Assert.IsTrue(obj2.destroyed);
      Assert.IsTrue(obj3.destroyed);
      Assert.AreEqual(obj.Handle, obj.destroyedHandle);
      Assert.AreEqual(obj2.Handle, obj2.destroyedHandle);
      Assert.AreEqual(obj3.Handle, obj3.destroyedHandle);
    }

    [Test]
    public void ComplexHierachyThreadedStressTest_Success()
    {
      const int countThreads = 20;
      const int countObjects = 1000;
      var objsArray = new TesterClass[countThreads, countObjects];
      for (var i = 0; i < countThreads; i++)
        for(var j = 0; j < countObjects; j++)
          objsArray[i, j] = new TesterClass(new IntPtr[] { });
      for (var i = 1; i < countThreads; i++)
        for (var j = 1; j < countObjects; j++)
        {
          TesterClass.UnmanagedObjectLifecycle.AddDependency(typeof(TesterClass).Name, objsArray[i - 1, j].Handle, typeof(TesterClass).Name, objsArray[i, j].Handle);
          TesterClass.UnmanagedObjectLifecycle.AddDependency(typeof(TesterClass).Name, objsArray[i, j].Handle, typeof(TesterClass).Name, objsArray[i - 1, j - 1].Handle);
        }
      var threads = new Thread[countThreads];
      for (var threadNo = 0; threadNo < countThreads; threadNo++)
      {
        var no = threadNo;
        threads[no] = new Thread(() =>
        {
          for (var i = 0; i < countObjects; i++)
          {
            objsArray[no, i].Dispose();
            Thread.Sleep(5);
          }
        });
      }
      foreach (var t in threads)
        t.Start();
      foreach (var t in threads)
        t.Join();
      Thread.Yield();
      Thread.Sleep(800);
      foreach (var o in objsArray)
      {
        Assert.IsTrue(o.destroyed);
        Assert.AreEqual(o.destroyedHandle, o.Handle);
      }
    }
  }
}
