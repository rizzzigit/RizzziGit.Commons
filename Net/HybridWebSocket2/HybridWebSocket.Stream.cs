using System;

namespace RizzziGit.Commons.Net.HybridWebSocket2;

using System.Runtime.ExceptionServices;
using Collections;
using Memory;

public sealed partial class HybridWebSocket
{
    public sealed class Stream(CancellationToken serviceCancellation)
    {
        public enum RedirectMode : byte
        {
            Error
        }

        public abstract class Entry
        {
            private Entry() { }

            public sealed class Feed : Entry
            {
                public required CompositeBuffer Buffer;
            }

            public sealed class Done : Entry;

            public sealed class Abort : Entry;

            public sealed class Redirect : Entry
            {
                public required RedirectMode RedirectMode;
                public required Stream Stream;
            }
        }

        public abstract class State
        {
            private State() { }

            public sealed class Feed() : State();

            public sealed class Done() : State();

            public sealed class Abort(Exception? exception = null) : State()
            {
                public Exception? Exception => exception;
            }

            public sealed class Redirect(RedirectMode redirectMode, Stream stream) : State()
            {
                public RedirectMode RedirectMode => redirectMode;
                public Stream Stream => stream;
            }
        }

        private readonly WaitQueue<Entry> waitQueue = new();
        private State state = new State.Feed();

        private void EnsureFeedState()
        {
            if (state is not State.Feed)
            {
                if (state is State.Abort abortState && abortState.Exception != null)
                {
                    ExceptionDispatchInfo.Throw(abortState.Exception);
                }

                throw new InvalidOperationException();
            }
        }

        public async Task<Stream> Push(
            CompositeBuffer buffer,
            CancellationToken cancellationToken = default
        )
        {
            long maxSize = 1024 * 8;
            if (buffer.Length > maxSize) {
                for (long offset = 0; offset < buffer.Length; offset += maxSize) {
                    await Push(buffer.Slice(offset, Math.Min(offset + maxSize, buffer.Length)), cancellationToken);
                }
            }

            EnsureFeedState();

            using CancellationTokenSource cancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCancellation);

            await waitQueue.Enqueue(new Entry.Feed { Buffer = buffer }, cancellationTokenSource.Token);

            return this;
        }

        public async Task<Stream> Finish(CancellationToken cancellationToken = default)
        {
            EnsureFeedState();

            using CancellationTokenSource cancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCancellation);

            await waitQueue.Enqueue(new Entry.Done(), cancellationTokenSource.Token);
            state = new State.Done();

            return this;
        }

        public async Task<Stream> Abort(
            Exception? exception = null,
            CancellationToken cancellationToken = default
        )
        {
            EnsureFeedState();

            using CancellationTokenSource cancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCancellation);

            await waitQueue.Enqueue(new Entry.Abort(), cancellationTokenSource.Token);
            state = new State.Abort();

            waitQueue.Dispose(exception);

            return this;
        }

        public async Task<Stream> Redirect(
            RedirectMode redirectMode,
            Stream? stream = default,
            CancellationToken cancellationToken = default
        )
        {
            EnsureFeedState();

            stream ??= new(serviceCancellation);

            using CancellationTokenSource cancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCancellation);

            await waitQueue.Enqueue(
                new Entry.Redirect { RedirectMode = redirectMode, Stream = stream },
                cancellationTokenSource.Token
            );

            state = new State.Redirect(redirectMode, stream);

            return stream;
        }

        public async Task<CompositeBuffer?> Shift(CancellationToken cancellationToken)
        {
            if (waitQueue.Count == 0 && state is not State.Feed)
            {
                if (state is State.Abort abortState && abortState.Exception != null)
                {
                    ExceptionDispatchInfo.Throw(abortState.Exception);
                }

                throw new InvalidOperationException();
            }

            using CancellationTokenSource cancellationTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCancellation);

            Entry item = await waitQueue.Dequeue(cancellationTokenSource.Token);

            if (item is Entry.Feed feed)
                return feed.Buffer;
            else if (item is Entry.Done)
                return null;
            else if (item is Entry.Abort)
                throw new OperationCanceledException();
            else if (item is Entry.Redirect error)
                throw new StreamRedirectException(error.RedirectMode, error.Stream);

            throw new InvalidOperationException();
        }
    }

    public sealed class StreamRedirectException(Stream.RedirectMode mode, Stream stream)
        : Exception("Stream error", null)
    {
        public Stream.RedirectMode RedirectMode => mode;
        public Stream Stream => stream;
    }
}
