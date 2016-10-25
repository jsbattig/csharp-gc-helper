using System;

namespace GChelpers
{
  [Serializable]
  public class EGChelper : Exception
  {
    public EGChelper() { }

    public EGChelper(string msg) : base(msg) { }
  }

  [Serializable]
  public class EInvalidRefCount<THandleType> : EGChelper
  {
    public EInvalidRefCount(string typeName, THandleType obj, int refCount) : base(string.Format("Invalid refcount value reached: {0} ({1} {2})", refCount, typeName, obj)) { }
  }

  [Serializable]
  public class EObjectNotFound<THandleType> : EGChelper
  {
    public EObjectNotFound(string typeName, THandleType obj) : base(string.Format("Object not found ({0} {1})", typeName, obj)) { }
  }

  [Serializable]
  public class EFailedObjectRemoval<THandleType> : EGChelper
  {
    public EFailedObjectRemoval(string typeName, THandleType obj) : base(string.Format("Failed to remove object ({0} {1})", typeName, obj)) { }
  }

  [Serializable]
  public class EDependencyObjectNotFound<THandleType> : EObjectNotFound<THandleType>
  {
    public EDependencyObjectNotFound(string typeName, THandleType obj) : base(typeName, obj) { }
  }

  [Serializable]
  public class EDependencyNotFound<THandleType> : EGChelper
  {
    public EDependencyNotFound(string typeName, THandleType obj) : base(string.Format("Dependency not found ({0} {1})", typeName, obj)) { }
  }
}