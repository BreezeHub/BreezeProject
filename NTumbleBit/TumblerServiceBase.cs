using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using NTumbleBit.Logging;

namespace NTumbleBit
{
    public abstract class TumblerServiceBase : ITumblerService
	{
		private CancellationToken _Stop;
		private CancellationTokenSource _StopSource;
		private TaskCompletionSource<bool> _Stopping;
		public abstract string Name { get; }
        public bool IsTumbling { get; set; }

        public void Start()
		{
			if(Started)
				throw new InvalidOperationException("Service already started");
			_Stopping = new TaskCompletionSource<bool>();
			_StopSource = new CancellationTokenSource();
			_Stop = _StopSource.Token;
			StartCore(_Stop);
            IsTumbling = true;
        }

		protected abstract void StartCore(CancellationToken cancellationToken);

		protected void Stopped()
		{
			_Stopping.SetResult(true);
		}

		public bool Started
		{
			get
			{
				return _Stopping != null && !_Stopping.Task.IsCompleted;
			}
		}

		public Task Stop()
		{
            IsTumbling = false;
            if (!Started)
				throw new InvalidOperationException("Service already stopped");
			if(!_StopSource.IsCancellationRequested)
			{
				_StopSource.Cancel();
				return _Stopping.Task;
			}
            return Task.CompletedTask;
		}
	}
}
