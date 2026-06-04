using System.Collections.Immutable;
using System.ComponentModel;

namespace CSStack.TADA
{
    /// <summary>
    /// Exception class that manages multiple exceptions by key.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <remarks>
    /// Constructor
    /// </remarks>
    /// <param name="innerExceptions"></param>
    /// <param name="message"></param>
    /// <param name="innerException"></param>
    [Obsolete(
        "This exception is deprecated. Domain models should use standard exceptions " + "(e.g., ArgumentException) for guard clauses. Aggregating multiple exceptions "
        + "for UX purposes should be handled in the application or presentation layer.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class KeyedMultiReasonException<TKey>(
        ImmutableDictionary<TKey, Exception> innerExceptions,
        string? message = null,
        Exception? innerException = null)
        : Exception(message, innerException)
        where TKey : Enum
    {
        /// <summary>
        /// Dictionary that manages exceptions that occurred simultaneously by key.
        /// </summary>
        public ImmutableDictionary<TKey, Exception> Exceptions { get; private set; } = innerExceptions;

        /// <summary>
        /// Add an exception.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="exception"></param>
        public void AddException(TKey key, Exception exception)
        {
            Exceptions = Exceptions.Add(key, exception);
        }
    }
}
