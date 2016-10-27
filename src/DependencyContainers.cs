using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace GChelpers
{
  public class ConcurrentDependencies<THandleClass, THandle> : IEnumerable<Tuple<THandleClass, THandle>>
  {
    private readonly ConcurrentDictionary<Tuple<THandleClass, THandle>, int> _container = new ConcurrentDictionary<Tuple<THandleClass, THandle>, int>();

    public bool Add(THandleClass handleClass, THandle dep)
    {
      return _container.TryAdd(new Tuple<THandleClass, THandle>(handleClass, dep), 0);
    }

    public bool Remove(THandleClass handleClass, THandle dep)
    {
      int dummy;
      return _container.TryRemove(new Tuple<THandleClass, THandle>(handleClass, dep), out dummy);
    }

    public IEnumerator<Tuple<THandleClass, THandle>> GetEnumerator()
    {
      foreach (var dep in _container)
        yield return dep.Key;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }
}