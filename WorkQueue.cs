using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

public class WorkQueue : WorkQueue<Action>
{
    public WorkQueue() : this(16, -1) { }
    public WorkQueue(int thread)
        : this(thread, -1)
    {
    }
    public WorkQueue(int thread, int capacity)
    {
        base.Thread = thread;
        base.Capacity = capacity;
        base.Process += delegate (Action ah) {
            ah();
        };
    }
}

public class WorkQueue<T> : IDisposable
{
    public delegate void WorkQueueProcessHandler(T item);
    public event WorkQueueProcessHandler Process;

    private int _thread = 16;
    private int _capacity = -1;
    private int _work_index = 0;
    private ConcurrentDictionary<int, WorkInfo> _works = new ConcurrentDictionary<int, WorkInfo>();
    private ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();

    public WorkQueue() : this(16, -1) { }
    public WorkQueue(int thread)
        : this(thread, -1)
    {
    }
    public WorkQueue(int thread, int capacity)
    {
        _thread = thread;
        _capacity = capacity;
    }

    public void Enqueue(T item)
    {
        if (_capacity > 0 && _queue.Count >= _capacity) throw new Exception($"队列数量大于(capacity)设置值：{_capacity}");
        _queue.Enqueue(item);
        foreach (WorkInfo w in _works.Values)
        {
            if (w.IsWaiting)
            {
                w.Set();
                return;
            }
        }
        if (_works.Count < _thread)
        {
            if (_queue.Count > 0)
            {
                int index = Interlocked.Increment(ref _work_index);
                var work = new WorkInfo();
                _works.TryAdd(index, work);

                new Thread(delegate () {
                    while (true)
                    {
                        if (_queue.TryDequeue(out var de))
                        {
                            try
                            {
                                this.OnProcess(de);
                            }
                            catch
                            {
                            }
                        }
                        if (_queue.Count == 0)
                        {
                            work.WaitOne(TimeSpan.FromSeconds(20));

                            if (_queue.Count == 0)
                                break;
                        }
                    }
                    _works.TryRemove(index, out var oldw);
                    work.Dispose();
                }).Start();
            }
        }
    }

    protected virtual void OnProcess(T item)
    {
        if (Process != null)
            Process(item);
    }

    #region IDisposable 成员

    public void Dispose()
    {
        while (_queue.TryDequeue(out var de)) ;
        foreach (WorkInfo w in _works.Values)
            w.Dispose();
    }

    #endregion

    public int Thread
    {
        get => _thread;
        set
        {
            if (_thread != value)
                _thread = value;
        }
    }
    public int Capacity
    {
        get => _capacity;
        set
        {
            if (_capacity != value)
                _capacity = value;
        }
    }

    public int UsedThread => _works.Count;
    public int Queue => _queue.Count;

    public string Statistics
    {
        get
        {
            string value = string.Format(@"线程：{0}/{1}
队列：{2}

", _works.Count, _thread, _queue.Count);
            foreach (var k in _works.Keys)
            {
                WorkInfo w = null;
                if (_works.TryGetValue(k, out w))
                    value += string.Format(@"线程{0}：{1}
", k, w.IsWaiting);
            }
            return value;
        }
    }

    class WorkInfo : IDisposable
    {
        private ManualResetEvent _reset = new ManualResetEvent(false);
        private bool _isWaiting = false;

        public void WaitOne(TimeSpan timeout)
        {
            try
            {
                _reset.Reset();
                _isWaiting = true;
                _reset.WaitOne(timeout);
            }
            catch { }
        }
        public void Set()
        {
            try
            {
                _isWaiting = false;
                _reset.Set();
            }
            catch { }
        }

        public bool IsWaiting => _isWaiting;
        public void Dispose() => this.Set();
    }
}
