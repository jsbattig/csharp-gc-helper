using System;

namespace GChelpers
{
  [Serializable]
  public class EDisposeHelper : Exception
  {
    public EDisposeHelper() { }

    public EDisposeHelper(string msg) : base(msg) { }
  }

  [Serializable]
  public class EObjectNotFound<THandleType> : EDisposeHelper
  {
    public EObjectNotFound(string typeName, THandleType obj) : base(string.Format("Object not found ({0} {1})", typeName, obj)) { }
  }

  [Serializable]
  public class EFailedObjectRemoval<THandleType> : EDisposeHelper
  {
    public EFailedObjectRemoval(string typeName, THandleType obj) : base(string.Format("Failed to remove object ({0} {1})", typeName, obj)) { }
  }

  [Serializable]
  public class EDependencyObjectNotFound<THandleType> : EObjectNotFound<THandleType>
  {
    public EDependencyObjectNotFound(string typeName, THandleType obj) : base(typeName, obj) { }
  }

  [Serializable]
  public class EDependencyNotFound<THandleType> : EDisposeHelper
  {
    public EDependencyNotFound(string typeName, THandleType obj) : base(string.Format("Dependency not found ({0} {1})", typeName, obj)) { }
  }
}