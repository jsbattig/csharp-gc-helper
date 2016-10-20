using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace JSB.GChelpers
{
  public class ConcurrentDependencies<THandleType> : IEnumerable<THandleType>
  {
    private readonly ConcurrentDictionary<THandleType, int> _container = new ConcurrentDictionary<THandleType, int>();

    public void Add(THandleType dep)
    {
      _container.TryAdd(dep, 0);
    }

    public void Remove(THandleType dep)
    {
      int value;
      _container.TryRemove(dep, out value);
    }

    public IEnumerator<THandleType> GetEnumerator()
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