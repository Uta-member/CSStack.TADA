namespace CSStack.TADA
{
    /// <summary>
    /// Exception class for invalid value objects.
    /// </summary>
    public class ValueObjectInvalidException : TADAException
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="innserException">Inner exception</param>
        public ValueObjectInvalidException(string? message = null, Exception? innserException = null)
            : base(message, innserException)
        {
        }
    }
}
