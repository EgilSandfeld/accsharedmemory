using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AssettoCorsaSharedMemory;

public static class UdpClientExtensions
{
    public static async Task<UdpReceiveResult> WithCancellation(this Task<UdpReceiveResult> task, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<bool>();
        using (token.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
        {
            if (task != await Task.WhenAny(task, tcs.Task))
            {
                throw new OperationCanceledException(token);
            }
        }
        return await task;
    }
}