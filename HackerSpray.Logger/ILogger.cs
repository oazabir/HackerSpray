using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Copying the ILogger implementation from .net core in order to 
/// make the logging code same in .net 4 and .net core
/// </summary>
namespace Microsoft.Extensions.Logging
{
    public struct EventId
    {
        public EventId(int id, string name = null)
        {
            this.Id = id;
            this.Name = name;
        }



        public int Id { get; }
        public string Name { get; }

        public static implicit operator EventId(int i)
        {
            return new EventId(i);
        }
    }
    //
    // Summary:
    //     Defines logging severity levels.
    public enum LogLevel
    {
        //
        // Summary:
        //     Logs that contain the most detailed messages. These messages may contain sensitive
        //     application data. These messages are disabled by default and should never be
        //     enabled in a production environment.
        Trace = 0,
        //
        // Summary:
        //     Logs that are used for interactive investigation during development. These logs
        //     should primarily contain information useful for debugging and have no long-term
        //     value.
        Debug = 1,
        //
        // Summary:
        //     Logs that track the general flow of the application. These logs should have long-term
        //     value.
        Information = 2,
        //
        // Summary:
        //     Logs that highlight an abnormal or unexpected event in the application flow,
        //     but do not otherwise cause the application execution to stop.
        Warning = 3,
        //
        // Summary:
        //     Logs that highlight when the current flow of execution is stopped due to a failure.
        //     These should indicate a failure in the current activity, not an application-wide
        //     failure.
        Error = 4,
        //
        // Summary:
        //     Logs that describe an unrecoverable application or system crash, or a catastrophic
        //     failure that requires immediate attention.
        Critical = 5,
        //
        // Summary:
        //     Not used for writing log messages. Specifies that a logging category should not
        //     write any messages.
        None = 6
    }
    //
    // Summary:
    //     Represents a type used to perform logging.
    //
    // Remarks:
    //     Aggregates most logging patterns to a single method.
    public interface ILogger
    {
        //
        // Summary:
        //     Begins a logical operation scope.
        //
        // Parameters:
        //   state:
        //     The identifier for the scope.
        //
        // Returns:
        //     An IDisposable that ends the logical operation scope on dispose.
        IDisposable BeginScope<TState>(TState state);
        //
        // Summary:
        //     Checks if the given logLevel is enabled.
        //
        // Parameters:
        //   logLevel:
        //     level to be checked.
        //
        // Returns:
        //     true if enabled.
        bool IsEnabled(LogLevel logLevel);
        //
        // Summary:
        //     Writes a log entry.
        //
        // Parameters:
        //   logLevel:
        //     Entry will be written on this level.
        //
        //   eventId:
        //     Id of the event.
        //
        //   state:
        //     The entry to be written. Can be also an object.
        //
        //   exception:
        //     The exception related to this entry.
        //
        //   formatter:
        //     Function to create a string message of the state and exception.
        void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter);
    }
}
