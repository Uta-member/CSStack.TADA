namespace CSStack.TADA
{
    /// <summary>
    /// Exception thrown when an invalid operation is performed within the domain.
    /// </summary>
    public class DomainInvalidOperationException : TADAException
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="innserException">Inner exception</param>
        public DomainInvalidOperationException(string? message = null, Exception? innserException = null)
            : base(message, innserException)
        {
        }
    }
}
