using System.ComponentModel;

namespace CSStack.TADA
{
    /// <summary>
    /// Base class for value objects.
    /// </summary>
    [Obsolete(
        "ValueObjectBase is deprecated. Please implement IValueObject directly. "
        + "For IsInvalidValue functionality, use ValueObjectExtensions instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract record ValueObjectBase : IValueObject
    {
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

        /// <inheritdoc/>
        public abstract void Validate();
    }
}
