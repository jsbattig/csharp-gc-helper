using System;
using System.Collections.Concurrent;
using System.Threading;

namespace GChelpers
{
  public class UnregistrationAgent<THandleClass, THandleType> : IDisposable
  {
    private readonly Thread _unregistrationThread;
    private readonly IHandleRemover<THandleClass, THandleType> _handleRemover;
    private bool _requestedStop;
    private readonly ConcurrentQueue<UnmanagedObjectGCHelper<THandleClass, THandleType>.HandleContainer> _unregistrationQueue;
    private readonly AutoResetEvent _eventWaitHandle;

    internal UnregistrationAgent(IHandleRemover<THandleClass, THandleType> handleRemover)
    {
      _handleRemover = handleRemover;
      _eventWaitHandle = new AutoResetEvent(false);
      _unregistrationQueue = new ConcurrentQueue<UnmanagedObjectGCHelper<THandleClass, THandleType>.HandleContainer>();
      _unregistrationThread = new Thread(Run);
      _unregistrationThread.Start();
    }

    private void Dispose(bool disposing)
    {
      if (!disposing)
        return;
      Stop();
      _eventWaitHandle.Dispose();
    }

    public void Dispose()
    {
      Dispose(true);
    }

    public void Enqueue(THandleClass handleClass, THandleType handle)
    {
      if (_requestedStop)
        return;
      _unregistrationQueue.Enqueue(new UnmanagedObjectGCHelper<THandleClass, THandleType>.HandleContainer(handleClass, handle));
      _eventWaitHandle.Set();
    }

    public void Run()
    {
      while (true)
      {
        UnmanagedObjectGCHelper<THandleClass, THandleType>.HandleContainer dequeuedHandleContainer;
        if (!_unregistrationQueue.TryDequeue(out dequeuedHandleContainer))
        {
          if (_requestedStop)
            return;
          _eventWaitHandle.WaitOne();
          continue;
        }
        _handleRemover.RemoveAndDestroyHandle(dequeuedHandleContainer.Item1, dequeuedHandleContainer.Item2);
      }
    }

    public void Stop()
    {
      if (_requestedStop)
        return;
      _requestedStop = true;
      _eventWaitHandle.Set();
      _unregistrationThread.Join();
    }
  }
}