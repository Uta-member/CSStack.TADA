namespace CSStack.TADA
{
    /// <summary>
    /// Base exception class for TADA.
    /// </summary>
    public class TADAException : Exception
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="innserException">Inner exception</param>
        public TADAException(string? message = null, Exception? innserException = null)
            : base(message, innserException)
        {
        }
    }
}
