using System;

namespace JSB.GChelpers
{
  public class EDisposeHelper : Exception
  {
    public EDisposeHelper() { }

    public EDisposeHelper(string msg) : base(msg) { }
  }

  public class EObjectNotFound<THandleType> : EDisposeHelper
  {
    public EObjectNotFound(THandleType obj) : base(string.Format("Object not found ({0})", obj)) { }
  }

  public class EDependencyNotFound<THandleType> : EDisposeHelper
  {
    public EDependencyNotFound(THandleType obj) : base(string.Format("Dependency not found ({0})", obj)) { }
  }

  public class EObjectAlreadyExists<THandleType> : EDisposeHelper
  {
    public EObjectAlreadyExists(THandleType obj) : base(string.Format("Object already exists ({0})", obj)) { }
  }
}