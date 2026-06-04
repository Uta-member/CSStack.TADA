namespace CSStack.TADA
{
    /// <summary>
    /// Interface for value objects.
    /// </summary>
    public interface IValueObject
    {
        /// <summary>
        /// Validate the value. Throws an exception when the value is invalid.
        /// </summary>
        void Validate();
    }
}
