namespace CSStack.TADA
{
    /// <summary>
    /// Interface for single value objects with predefined length constraints.
    /// </summary>
    public interface ILengthDefinedSingleValueObject
    {
        /// <summary>
        /// Maximum length.
        /// </summary>
        static abstract int MaxLength { get; }

        /// <summary>
        /// Minimum length.
        /// </summary>
        static abstract int MinLength { get; }
    }
}
