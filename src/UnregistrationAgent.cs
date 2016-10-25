using System;
using System.Collections.Concurrent;
using System.Threading;

namespace GChelpers
{
  public class UnregistrationAgent<THandleType> : IDisposable
  {
    private readonly Thread _unregistrationThread;
    private readonly IHandleRemover<THandleType> _handleRemover;
    private bool _requestedStop;
    private readonly ConcurrentQueue<UnmanagedObjectGCHelper<THandleType>.ClassNameHandlePair> _unregistrationQueue;
    private readonly AutoResetEvent _eventWaitHandle;

    internal UnregistrationAgent(IHandleRemover<THandleType> handleRemover)
    {
      _handleRemover = handleRemover;
      _eventWaitHandle = new AutoResetEvent(false);
      _unregistrationQueue = new ConcurrentQueue<UnmanagedObjectGCHelper<THandleType>.ClassNameHandlePair>();
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

    public void Enqueue(string className, THandleType handle)
    {
      if (_requestedStop)
        return;
      _unregistrationQueue.Enqueue(new UnmanagedObjectGCHelper<THandleType>.ClassNameHandlePair(className, handle));
      _eventWaitHandle.Set();
    }

    public void Run()
    {
      while (true)
      {
        UnmanagedObjectGCHelper<THandleType>.ClassNameHandlePair dequeuedClassNameHandlePair;
        if (!_unregistrationQueue.TryDequeue(out dequeuedClassNameHandlePair))
        {
          if (_requestedStop)
            return;
          _eventWaitHandle.WaitOne();
          continue;
        }
        _handleRemover.RemoveAndDestroyHandle(dequeuedClassNameHandlePair.Item1, dequeuedClassNameHandlePair.Item2);
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