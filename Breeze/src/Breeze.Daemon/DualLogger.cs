using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NTumbleBit.Logging;

namespace Breeze.Daemon
{
    class DualScopeDisposable : IDisposable
    {
        private IDisposable disposable1;
        private IDisposable disposable2;

        public DualScopeDisposable(IDisposable disposable1, IDisposable disposable2)
        {
            this.disposable1 = disposable1;
            this.disposable2 = disposable2;
        }

        public void Dispose()
        {
            this.disposable1.Dispose();
            this.disposable2.Dispose();
        }
    }

    //ordinarily this feature of adding multiple logger destinations is provided by a logger factory
    //however NTumbleBit does not support a logger factory and instead holds multiple ILogger(s)
    //this solution adds a second logger destination into NTumbleBit without needing to modify ntumblebit. 
    internal class DualLogger : ILogger
    {
        ILogger consoleLogger;
        ILogger nLogger; 

        public DualLogger(string name, Func<string, LogLevel, bool> filter, bool includeScopes)
        {
            var loggerProcessor = new ConsoleLoggerProcessor();

            this.consoleLogger = new CustomerConsoleLogger(name, filter, includeScopes, loggerProcessor);

            NLogLoggerProvider nLoggerProvider = new NLogLoggerProvider();
            this.nLogger = nLoggerProvider.CreateLogger(name);
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            IDisposable disposable1 = this.consoleLogger.BeginScope<TState>(state);
            IDisposable disposable2 = this.consoleLogger.BeginScope<TState>(state);

            return new DualScopeDisposable(disposable1, disposable2);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            this.consoleLogger.Log(logLevel, eventId, state, exception, formatter);
            this.nLogger.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
