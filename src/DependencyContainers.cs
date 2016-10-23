using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace GChelpers
{
  public class ConcurrentDependencies<THandleType> : IEnumerable<Tuple<string, THandleType>>
  {
    private readonly ConcurrentDictionary<Tuple<string, THandleType>, int> _container = new ConcurrentDictionary<Tuple<string, THandleType>, int>();

    public bool Add(string typeName, THandleType dep)
    {
      return _container.TryAdd(new Tuple<string, THandleType>(typeName, dep), 0);
    }

    public bool Remove(string typeName, THandleType dep)
    {
      int dummy;
      return _container.TryRemove(new Tuple<string, THandleType>(typeName, dep), out dummy);
    }

    public bool Find(string typeName, THandleType dep)
    {
      int dummy;
      return _container.TryGetValue(new Tuple<string, THandleType>(typeName, dep), out dummy);
    }

    public IEnumerator<Tuple<string, THandleType>> GetEnumerator()
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