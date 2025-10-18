namespace CSStack.TADA
{
    /// <summary>
    /// Exception thrown when the object does not exist.
    /// </summary>
    public class ObjectNotFoundException : TADAException
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="innserException">Inner exception</param>
        public ObjectNotFoundException(string? message = null, Exception? innserException = null)
            : base(message, innserException)
        {
        }
    }
}
