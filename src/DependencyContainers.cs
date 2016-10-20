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
      if(!_container.TryAdd(dep, 0))
        throw new EObjectDependencyAlreadyExists<THandleType>(dep);
    }

    public bool Remove(THandleType dep)
    {
      int dummy;
      return _container.TryRemove(dep, out dummy);
    }

    public bool Find(THandleType dep)
    {
      int dummy;
      return _container.TryGetValue(dep, out dummy);
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

  public class Dependencies<THandleType>
  {
    private readonly Dictionary<THandleType, int> _container = new Dictionary<THandleType, int>();

    public void Add(THandleType dep)
    {
      _container.Add(dep, 0);
    }

    public void Remove(THandleType dep)
    {
      _container.Remove(dep);
    }

    public bool Find(THandleType dep)
    {
      int dummy;
      return _container.TryGetValue(dep, out dummy);
    }
  }
}