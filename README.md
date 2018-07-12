轻便线程队列处理器

# 使用方法

> Install-Package WorkQueue

```csharp
//暂时使用线程池，并发量大后再考虑mq
static Lazy<WorkQueue<Bet_orderInfo>> wqlazy = new Lazy<WorkQueue<Bet_orderInfo>>(() => {
    var q = new WorkQueue<Bet_orderInfo>(10);
    q.Process += order => {
        try {
            order.执行业务();
        } catch {
            System.Threading.Thread.CurrentThread.Join(TimeSpan.FromSeconds(3));
            q.Enqueue(order); //等待3秒后，重新入队列执行
        }
    };
    //程序重启后加载新订单执行队列
    Bet_order.Select.WhereStatus(Et_statusENUM.NEW).ToList().ForEach(a => q.Enqueue(a));
    return q;
});
static WorkQueue<Bet_orderInfo> wq => wqlazy.Value;

//怎么入队列？
wq.Enqueue(对象); //对象作为参数传递给执行器

//也可以使用无参数方式
WorkQueue wq2 = new WorkQueue();
wq2.Enqueue(() => {
    //执行过程
});
```
