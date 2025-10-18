namespace CSStack.TADA
{
    /// <summary>
    /// Exception thrown when the object already exists.
    /// </summary>
    public class ObjectAlreadyExistException : TADAException
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="innserException">Inner exception</param>
        public ObjectAlreadyExistException(string? message = null, Exception? innserException = null)
            : base(message, innserException)
        {
        }
    }
}
