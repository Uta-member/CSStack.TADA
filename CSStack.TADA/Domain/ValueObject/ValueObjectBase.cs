namespace CSStack.TADA
{
    /// <summary>
    /// 値オブジェクトの基底クラス
    /// </summary>
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
