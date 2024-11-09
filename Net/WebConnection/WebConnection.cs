using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.ExceptionServices;

namespace RizzziGit.Commons.Net.WebConnection;

using Collections;
using Logging;
using Memory;
using RizzziGit.Commons.Utilities;
using Services;

public record class WebConnectionOptions
{
    public string Name = "Web Connection";
    public Logger? Logger = null;
}

public sealed class WebConnectionRequest(TaskCompletionSource<CompositeBuffer> source)
{
    public required CompositeBuffer Data;
    public required CancellationToken CancellationToken;

    public bool CanRespond => !source.Task.IsCompleted;

    public void SendResponse(CompositeBuffer bytes) => source.SetResult(bytes);

    public void SendErrorResponse(CompositeBuffer bytes) =>
        source.SetException(
            ExceptionDispatchInfo.SetCurrentStackTrace(new WebConnectionResponseException(bytes))
        );

    public void SendCancelResponse() => source.SetCanceled();
}

public class WebConnection(WebSocket webSocket, WebConnectionOptions options)
    : Service<WebConnection.WebConnectionContext>(options.Name, options.Logger)
{
    public record class WebConnectionContext
    {
        public required ConcurrentDictionary<
            uint,
            CancellationTokenSource
        > RequestCancellationTokenSources;
        public required ConcurrentDictionary<
            uint,
            TaskCompletionSource<CompositeBuffer>
        > ResponseTaskCompletionSources;

        public required bool ReceiveDone;
        public required WaitQueue<WebConnectionRequest?> Requests;
        public required WaitQueue<WorkerFeed> Feed;

        public required uint NextRequestId;
    }

    public abstract record WorkerFeed
    {
        private WorkerFeed() { }

        public sealed record Send(CompositeBuffer Bytes) : WorkerFeed;

        public sealed record Receive(CompositeBuffer Bytes) : WorkerFeed;

        public sealed record ReceiveDone() : WorkerFeed;

        public sealed record SendDone() : WorkerFeed;

        public sealed record Error(Exception Exception) : WorkerFeed;
    }

    private const byte PACKET_REQUEST = 0;
    private const byte PACKET_REQUEST_CANCEL = 1;
    private const byte PACKET_RESPONSE = 2;
    private const byte PACKET_RESPONSE_CANCEL = 3;
    private const byte PACKET_RESPONSE_ERROR = 4;
    private const byte PACKET_RESPONSE_INTERNAL_ERROR = 5;

    private const byte INTERNAL_ERROR_DUPLICATE_ID = 0;

    private async Task<CompositeBuffer?> Receive(
        WebConnectionContext context,
        CancellationToken cancellationToken
    )
    {
        if (context.ReceiveDone)
        {
            return null;
        }

        WebSocketReceiveResult? result = null;

        try
        {
            CompositeBuffer bytes = [];

            while (true)
            {
                while (true)
                {
                    byte[] buffer = new byte[4056];
                    result = await webSocket.ReceiveAsync(buffer, cancellationToken);
                    bytes.Append(buffer, 0, result.Count);

                    if (result.EndOfMessage)
                    {
                        break;
                    }
                }

                if (bytes.Length > 0)
                {
                    break;
                }
            }
            return bytes;
        }
        catch { }
        finally
        {
            if (result == null || result.CloseStatus != null)
            {
                context.ReceiveDone = true;
            }
        }

        return null;
    }

    private async Task Send(CompositeBuffer bytes, CancellationToken cancellationToken)
    {
        await webSocket.SendAsync(
            bytes.ToByteArray(),
            WebSocketMessageType.Binary,
            true,
            cancellationToken
        );
    }

    protected override Task<WebConnectionContext> OnStart(
        CancellationToken startupCancellationToken,
        CancellationToken serviceCancellationToken
    )
    {
        WebConnectionContext context =
            new()
            {
                RequestCancellationTokenSources = new(),
                ResponseTaskCompletionSources = new(),

                ReceiveDone = false,
                Requests = new(),
                Feed = new(),

                NextRequestId = 0,
            };

        return Task.FromResult(context);
    }

    protected override Task OnStop(WebConnectionContext context, ExceptionDispatchInfo? exception)
    {
        foreach (CancellationTokenSource source in context.RequestCancellationTokenSources.Values)
        {
            try
            {
                source.Cancel();
            }
            catch { }
        }

        foreach (
            TaskCompletionSource<CompositeBuffer> source in context
                .ResponseTaskCompletionSources
                .Values
        )
        {
            try
            {
                source.SetCanceled();
            }
            catch { }
        }

        return base.OnStop(context, exception);
    }

    protected override async Task OnRun(
        WebConnectionContext context,
        CancellationToken serviceCancellationToken
    )
    {
        Task[] tasks =
        [
            Task.Delay(-1, serviceCancellationToken),
            RunReceiveLoop(context, serviceCancellationToken),
            RunWorker(context, serviceCancellationToken)
        ];

        await Task.WhenAny(tasks);
        WaitTasksBeforeStopping.AddRange(tasks);
    }

    private async Task RunReceiveLoop(
        WebConnectionContext context,
        CancellationToken cancellationToken
    )
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CompositeBuffer? buffer = await Receive(context, cancellationToken);

            if (buffer == null)
            {
                await context.Feed.Enqueue(new WorkerFeed.ReceiveDone(), cancellationToken);
                return;
            }

            await context.Feed.Enqueue(new WorkerFeed.Receive(buffer), cancellationToken);
        }
    }

    private async Task RunWorker(WebConnectionContext context, CancellationToken cancellationToken)
    {
        List<Task> tasks = [];

        try
        {
            await foreach (WorkerFeed feed in context.Feed.WithCancellation(cancellationToken))
            {
                switch (feed)
                {
                    case WorkerFeed.Send(CompositeBuffer bytes):
                        await Send(bytes, cancellationToken);
                        break;

                    case WorkerFeed.Receive(CompositeBuffer bytes):
                    {
                        Task task = HandleReceivedPacket(context, bytes, cancellationToken);

                        async Task monitor()
                        {
                            lock (tasks)
                            {
                                tasks.Add(task);
                            }

                            try
                            {
                                await task;
                            }
                            catch (Exception exception)
                            {
                                await context.Feed.Enqueue(
                                    new WorkerFeed.Error(exception),
                                    CancellationToken.None
                                );
                            }
                            finally
                            {
                                lock (tasks)
                                {
                                    tasks.Remove(task);
                                }
                            }
                        }

                        _ = monitor();
                        break;
                    }

                    case WorkerFeed.ReceiveDone:
                        context.ReceiveDone = true;

                        await Task.WhenAll(tasks);
                        await context.Feed.Enqueue(
                            new WorkerFeed.SendDone(),
                            CancellationToken.None
                        );
                        break;

                    case WorkerFeed.SendDone:
                        return;

                    case WorkerFeed.Error(Exception exception):
                        ExceptionDispatchInfo.Throw(exception);
                        break;
                }
            }
        }
        finally
        {
            await context.Requests.Enqueue(null, CancellationToken.None);

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }
        }
    }

    private async Task HandleReceivedPacket(
        WebConnectionContext context,
        CompositeBuffer bytes,
        CancellationToken cancellationToken
    )
    {
        switch (bytes[0])
        {
            case PACKET_REQUEST:
            {
                using CancellationTokenSource cancellationTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                uint id = bytes.Slice(1, 5).ToUInt32();

                if (!context.RequestCancellationTokenSources.TryAdd(id, cancellationTokenSource))
                {
                    await context.Feed.Enqueue(
                        new WorkerFeed.Send(
                            CompositeBuffer.Concat(
                                PACKET_RESPONSE_INTERNAL_ERROR,
                                id,
                                INTERNAL_ERROR_DUPLICATE_ID
                            )
                        ),
                        CancellationToken.None
                    );
                    break;
                }

                CompositeBuffer result;

                TaskCompletionSource<CompositeBuffer> taskCompletionSource = new();
                try
                {
                    WebConnectionRequest request =
                        new(taskCompletionSource)
                        {
                            Data = bytes.Slice(5),
                            CancellationToken = cancellationTokenSource.Token
                        };

                    await context.Requests.Enqueue(request, cancellationToken);

                    result = await taskCompletionSource.Task;
                }
                catch (Exception exception)
                {
                    if (taskCompletionSource.Task.IsCanceled)
                    {
                        await context.Feed.Enqueue(
                            new WorkerFeed.Send(CompositeBuffer.Concat(PACKET_RESPONSE_CANCEL, id)),
                            CancellationToken.None
                        );
                    }
                    else
                    {
                        CompositeBuffer buffer = CompositeBuffer.Concat(
                            exception is WebConnectionResponseException responseException
                                ? responseException.ErrorData
                                : exception.Message
                        );

                        await context.Feed.Enqueue(
                            new WorkerFeed.Send(
                                CompositeBuffer.Concat(PACKET_RESPONSE_ERROR, id, buffer)
                            ),
                            CancellationToken.None
                        );
                    }

                    break;
                }

                await context.Feed.Enqueue(
                    new WorkerFeed.Send(CompositeBuffer.Concat(PACKET_RESPONSE, id, result)),
                    cancellationToken
                );

                context.RequestCancellationTokenSources.TryRemove(id, out _);
                break;
            }

            case PACKET_REQUEST_CANCEL:
            {
                uint id = bytes.Slice(1, 5).ToUInt32();

                if (
                    !context.RequestCancellationTokenSources.TryRemove(
                        id,
                        out CancellationTokenSource? source
                    )
                )
                {
                    break;
                }

                try
                {
                    source.Cancel();
                }
                catch { }

                break;
            }

            case PACKET_RESPONSE:
            {
                uint id = bytes.Slice(1, 5).ToUInt32();

                if (
                    !context.ResponseTaskCompletionSources.TryRemove(
                        id,
                        out TaskCompletionSource<CompositeBuffer>? source
                    )
                )
                {
                    break;
                }

                source.SetResult(bytes.Slice(5));
                break;
            }

            case PACKET_RESPONSE_CANCEL:
            {
                uint id = bytes.Slice(1, 5).ToUInt32();

                if (
                    !context.ResponseTaskCompletionSources.TryRemove(
                        id,
                        out TaskCompletionSource<CompositeBuffer>? source
                    )
                )
                {
                    break;
                }

                source.SetCanceled(CancellationToken.None);
                break;
            }

            case PACKET_RESPONSE_ERROR:
            {
                uint id = bytes.Slice(1, 5).ToUInt32();

                if (
                    !context.ResponseTaskCompletionSources.TryRemove(
                        id,
                        out TaskCompletionSource<CompositeBuffer>? source
                    )
                )
                {
                    break;
                }

                source.SetException(
                    ExceptionDispatchInfo.SetCurrentStackTrace(
                        new WebConnectionResponseException(bytes.Slice(5))
                    )
                );

                break;
            }

            case PACKET_RESPONSE_INTERNAL_ERROR:
            {
                uint id = bytes.Slice(1, 5).ToUInt32();

                if (
                    !context.ResponseTaskCompletionSources.TryRemove(
                        id,
                        out TaskCompletionSource<CompositeBuffer>? source
                    )
                )
                {
                    break;
                }

                source.SetException(
                    ExceptionDispatchInfo.SetCurrentStackTrace(
                        new WebConnectionInternalResponseException(bytes[5])
                    )
                );

                break;
            }
        }
    }

    public async Task<CompositeBuffer> SendRequest(
        CompositeBuffer bytes,
        CancellationToken cancellationToken
    )
    {
        WebConnectionContext context = GetContext();

        while (true)
        {
            TaskCompletionSource<CompositeBuffer> source = new();
            uint id = unchecked(context.NextRequestId++);

            if (!context.ResponseTaskCompletionSources.TryAdd(id, source))
            {
                continue;
            }

            await context.Feed.Enqueue(
                new WorkerFeed.Send(CompositeBuffer.Concat(PACKET_REQUEST, id, bytes)),
                cancellationToken
            );

            async void cancel()
            {
                await cancellationToken.GetTask();

                if (!source.Task.IsCompleted)
                {
                    try
                    {
                        await context.Feed.Enqueue(
                            new WorkerFeed.Send(CompositeBuffer.Concat(PACKET_REQUEST_CANCEL, id)),
                            CancellationToken.None
                        );
                    }
                    catch (Exception exception)
                    {
                        source.TrySetException(exception);
                    }
                }
            }

            cancel();

            try
            {
                return await source.Task;
            }
            catch (Exception exception)
            {
                if (exception is WebConnectionInternalResponseException a)
                {
                    if (a.Reason == INTERNAL_ERROR_DUPLICATE_ID)
                    {
                        continue;
                    }
                }

                throw;
            }
        }
    }

    public async Task<WebConnectionRequest?> ReceiveRequest(CancellationToken cancellationToken)
    {
        WebConnectionContext context = GetContext();

        if (context.ReceiveDone)
        {
            return null;
        }

        using CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            GetCancellationToken()
        );

        return await context.Requests.Dequeue(source.Token);
    }
}

public abstract class WebConnectionException(string? message, Exception? inner = null)
    : Exception(message, inner);

public sealed class WebConnectionResponseException : WebConnectionException
{
    public WebConnectionResponseException(CompositeBuffer errorData, Exception? inner = null)
        : base($"Remote returned an error.", inner)
    {
        ErrorData = errorData;
    }

    public readonly CompositeBuffer ErrorData;
}

public sealed class WebConnectionInternalResponseException : WebConnectionException
{
    internal WebConnectionInternalResponseException(byte reason, Exception? inner = null)
        : base($"Remote returned an internal error.", inner)
    {
        Reason = reason;
    }

    public readonly byte Reason;
}
