namespace CSStack.TADA
{
    /// <summary>
    /// 値オブジェクトの基底クラス
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
