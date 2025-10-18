using System.Collections.Immutable;

namespace CSStack.TADA
{
    /// <summary>
    /// Exception that aggregates multiple exceptions.
    /// </summary>
    public class MultiReasonException : Exception
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="exceptions"></param>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public MultiReasonException(
            ImmutableList<Exception> exceptions,
            string? message = null,
            Exception? innerException = null)
            : base(message, innerException)
        {
            Exceptions = exceptions;
        }

        /// <summary>
        /// Add an exception.
        /// </summary>
        /// <param name="exception">The exception to add.</param>
        public void AddException(Exception exception)
        {
            Exceptions = Exceptions.Add(exception);
        }

        /// <summary>
        /// List of exceptions that occurred simultaneously.
        /// </summary>
        public ImmutableList<Exception> Exceptions { get; protected set; }
    }
}
