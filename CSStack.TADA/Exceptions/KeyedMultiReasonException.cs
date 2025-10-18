using System.Collections.Immutable;

namespace CSStack.TADA
{
    /// <summary>
    /// Exception class that manages multiple exceptions by key.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    public class KeyedMultiReasonException<TKey> : Exception
        where TKey : Enum
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="innerExceptions"></param>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public KeyedMultiReasonException(
            ImmutableDictionary<TKey, Exception> innerExceptions,
            string? message = null,
            Exception? innerException = null)
            : base(message, innerException)
        {
            Exceptions = innerExceptions;
        }

        /// <summary>
        /// Add an exception.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="exception"></param>
        public void AddException(TKey key, Exception exception)
        {
            Exceptions = Exceptions.Add(key, exception);
        }

        /// <summary>
        /// Dictionary that manages exceptions that occurred simultaneously by key.
        /// </summary>
        public ImmutableDictionary<TKey, Exception> Exceptions { get; private set; }
    }
}
