namespace CSStack.TADA
{
    /// <summary>
    /// Entity interface.
    /// </summary>
    /// <typeparam name="TIdentifier">Entity identifier type</typeparam>
    public interface IEntity<TIdentifier>
        where TIdentifier : notnull
    {
        /// <summary>
        /// Validate.
        /// </summary>
        void Validate();

        /// <summary>
        /// Entity identifier.
        /// </summary>
        TIdentifier Identifier { get; }

        /// <summary>
        /// Indicates whether there is an invalid value.
        /// </summary>
        bool IsInvalidValue { get; }
    }
}
