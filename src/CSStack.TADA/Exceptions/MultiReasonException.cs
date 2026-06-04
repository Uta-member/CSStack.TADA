using System.Collections.Immutable;
using System.ComponentModel;

namespace CSStack.TADA
{
    /// <summary>
    /// Exception that aggregates multiple exceptions.
    /// </summary>
    [Obsolete(
        "This exception is deprecated. Domain models should use standard exceptions " + "(e.g., ArgumentException) for guard clauses. Aggregating multiple exceptions "
        + "for UX purposes should be handled in the application or presentation layer.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
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
        /// List of exceptions that occurred simultaneously.
        /// </summary>
        public ImmutableList<Exception> Exceptions { get; protected set; }

        /// <summary>
        /// Add an exception.
        /// </summary>
        /// <param name="exception">The exception to add.</param>
        public void AddException(Exception exception)
        {
            Exceptions = Exceptions.Add(exception);
        }
    }
}
