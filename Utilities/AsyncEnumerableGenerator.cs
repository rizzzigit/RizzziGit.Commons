using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace RizzziGit.Commons.Utilities;

using Collections;

public sealed class AsyncEnumerableSource<T> : IAsyncEnumerable<T>, IAsyncDisposable
{
    private abstract class Entry
    {
        private Entry() { }

        public class Item(T item) : Entry
        {
            public T Target => item;
        }

        public class Error(Exception exception) : Entry
        {
            public Exception Target => exception;
        }
    }

    public AsyncEnumerableSource()
    {
        WaitQueue = new();
    }

    private readonly WaitQueue<Entry> WaitQueue;

    public async IAsyncEnumerable<T> GetAsyncEnumerable(
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        await foreach (Entry entry in WaitQueue.WithCancellation(cancellationToken))
        {
            switch (entry)
            {
                case Entry.Item item:
                    yield return item.Target;
                    break;
                case Entry.Error exception:
                    ExceptionDispatchInfo.Throw(exception.Target);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
    }

    public async Task Push(T item, CancellationToken cancellationToken = default)
    {
        await WaitQueue.Enqueue(new Entry.Item(item), cancellationToken);
    }

    public async Task Except(Exception exception, CancellationToken cancellationToken = default)
    {
        await WaitQueue.Enqueue(new Entry.Error(exception), cancellationToken);
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        WaitQueue.Dispose();
        return new ValueTask();
    }

    IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(
        CancellationToken cancellationToken
    ) => GetAsyncEnumerable(cancellationToken).GetAsyncEnumerator(cancellationToken);
}
