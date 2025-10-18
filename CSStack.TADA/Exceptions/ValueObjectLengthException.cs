namespace CSStack.TADA
{
    /// <summary>
    /// Exception related to the length of a value object.
    /// </summary>
    public class ValueObjectLengthException : ValueObjectInvalidException
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="minLength">Minimum length</param>
        /// <param name="maxLength">Maximum length</param>
        /// <param name="currentLength">Current length</param>
        /// <param name="message">Message</param>
        /// <param name="innserException">Inner exception</param>
        public ValueObjectLengthException(
            int minLength,
            int maxLength,
            int currentLength,
            string? message = null,
            Exception? innserException = null)
            : base(message, innserException)
        {
            MinLength = minLength;
            MaxLength = maxLength;
            CurrentLength = currentLength;
        }

        /// <summary>
        /// Current length
        /// </summary>
        public int CurrentLength { get; }

        /// <summary>
        /// Maximum length
        /// </summary>
        public int MaxLength { get; }

        /// <summary>
        /// Minimum length
        /// </summary>
        public int MinLength { get; }
    }
}
