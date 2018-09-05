using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Breeze.BreezeServer.Features.Masternode.Services.FullNodeBatches
{
    public abstract class BatchBase<T, TResult>
    {
        public TimeSpan BatchInterval
        {
            get; set;
        }

        public int BatchCount
        {
            get
            {
                return Data.Count;
            }
        }
        protected ConcurrentQueue<T> Data = new ConcurrentQueue<T>();

        public async Task<TResult> WaitTransactionAsync(T data)
        {
            var isFirstOutput = false;
            TaskCompletionSource<TResult> completion = null;
            lock (Data)
            {
                completion = _TransactionCreated;
                Data.Enqueue(data);
                isFirstOutput = Data.Count == 1;
            }
            if (isFirstOutput)
            {
                await Task.WhenAny(completion.Task, Task.Delay(BatchInterval)).ConfigureAwait(false);
                if (completion.Task.Status != TaskStatus.RanToCompletion &&
                    completion.Task.Status != TaskStatus.Faulted)
                {
                    await MakeTransactionAsync().ConfigureAwait(false);
                }
            }
            return await completion.Task.ConfigureAwait(false);
        }

        protected async Task MakeTransactionAsync()
        {

            List<T> data = new List<T>();
            T output = default(T);
            TaskCompletionSource<TResult> completion = null;
            lock (Data)
            {
                completion = _TransactionCreated;
                _TransactionCreated = new TaskCompletionSource<TResult>();
                while (Data.TryDequeue(out output))
                {
                    data.Add(output);
                }
            }
            if (data.Count == 0)
                return;
            var dataArray = data.ToArray();
            NBitcoin.Utils.Shuffle(dataArray);

            try
            {
                completion.TrySetResult(await RunAsync(dataArray).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }

        protected abstract Task<TResult> RunAsync(T[] data);

        TaskCompletionSource<TResult> _TransactionCreated = new TaskCompletionSource<TResult>();
    }
}
