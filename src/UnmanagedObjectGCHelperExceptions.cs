using System;

namespace GChelpers
{
  [Serializable]
  // ReSharper disable once InconsistentNaming
  public class EGChelper : Exception
  {
    public EGChelper() { }

    public EGChelper(string msg) : base(msg) { }
  }

  [Serializable]
  public class EInvalidRefCount<THandleClass, THandleType> : EGChelper
  {
    public EInvalidRefCount(THandleClass handleClass, THandleType obj, int refCount) : base(string.Format("Invalid refcount value reached: {0} ({1} {2})", refCount, handleClass, obj)) { }
  }

  [Serializable]
  public class EObjectNotFound<THandleClass, THandleType> : EGChelper
  {
    public EObjectNotFound(THandleClass handleClass, THandleType obj) : base(string.Format("Object not found ({0} {1})", handleClass, obj)) { }
  }

  [Serializable]
  public class EFailedObjectRemoval<THandleClass, THandleType> : EGChelper
  {
    public EFailedObjectRemoval(THandleClass handleClass, THandleType obj) : base(string.Format("Failed to remove object ({0} {1})", handleClass, obj)) { }
  }

  [Serializable]
  public class EParentObjectNotFound<THandleClass, THandleType> : EObjectNotFound<THandleClass, THandleType>
  {
    public EParentObjectNotFound(THandleClass handleClass, THandleType obj) : base(handleClass, obj) { }
  }

  [Serializable]
  public class EParentNotFound<THandleClass, THandleType> : EGChelper
  {
    public EParentNotFound(THandleClass handleClass, THandleType obj) : base(string.Format("Dependency not found ({0} {1})", handleClass, obj)) { }
  }
}