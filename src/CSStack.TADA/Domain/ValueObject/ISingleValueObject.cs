namespace CSStack.TADA
{
    /// <summary>
    /// Interface for value objects that hold a single value.
    /// </summary>
    /// <typeparam name="TValue">Value type</typeparam>
    /// <typeparam name="TSelf">Value object type</typeparam>
    public interface ISingleValueObject<TValue, TSelf> : IValueObject
        where TSelf : ISingleValueObject<TValue, TSelf>
    {
        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="value">Value</param>
        /// <returns></returns>
        static abstract TSelf Create(TValue value);

        /// <summary>
        /// Reconstruct from a repository.
        /// </summary>
        /// <param name="value">Value</param>
        /// <returns></returns>
        static abstract TSelf Reconstruct(TValue value);

        /// <summary>
        /// Underlying value.
        /// </summary>
        TValue Value { get; }
    }
}
