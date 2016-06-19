using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HackerSpray.Logger
{
    public class TraceLogger : ILogger
    {
        private static TraceSource _TraceSource = new TraceSource("HackerSpray");
        
        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            TraceEventType eventType = LogLevel2TraceEventType(logLevel);

            return _TraceSource.Switch.ShouldTrace(eventType);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            TraceEventType eventType = LogLevel2TraceEventType(logLevel);

            if (IsEnabled(logLevel))
            {
                string tracemessage = DateTime.Now.ToString("MMM dd HH:mm:ss") + ' ' +
                    Environment.MachineName + ' ' +
                    _TraceSource.Name + ':' + ' ' +
                    '[' + eventType + ']' + ' ' +
                    formatter(state, exception);
                foreach (TraceListener listener in _TraceSource.Listeners)
                {
                    listener.WriteLine(tracemessage);
                    listener.Flush();
                }
            }
        }

        private static TraceEventType LogLevel2TraceEventType(LogLevel logLevel)
        {
            return logLevel == LogLevel.Debug ? TraceEventType.Verbose :
                            logLevel == LogLevel.Error ? TraceEventType.Error :
                            logLevel == LogLevel.Warning ? TraceEventType.Warning :
                            logLevel == LogLevel.Critical ? TraceEventType.Critical :
                            logLevel == LogLevel.Information ? TraceEventType.Information :
                            TraceEventType.Verbose;
        }
    }
}
