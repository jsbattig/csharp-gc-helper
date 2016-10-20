using System;

namespace JSB.GChelpers
{
  public class EDisposeHelper : Exception
  {
  }

  public class EDisposeHelperObjectNotFound : EDisposeHelper
  {
  }

  public class EDependencyNotFound : EDisposeHelper
  {
  }
}