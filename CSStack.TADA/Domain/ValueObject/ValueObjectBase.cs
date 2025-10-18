namespace CSStack.TADA
{
    /// <summary>
    /// Base class for value objects.
    /// </summary>
    public abstract record ValueObjectBase : IValueObject
    {
        /// <inheritdoc/>
        public abstract void Validate();

        /// <inheritdoc/>
        public bool IsInvalidValue
        {
            get
            {
                try
                {
                    Validate();
                    return false;
                }
                catch
                {
                    return true;
                }
            }
        }
    }
}
