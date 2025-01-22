namespace CSStack.TADA
{
    /// <summary>
    /// 値オブジェクトのインターフェース
    /// </summary>
    public interface IValueObject
    {
        /// <summary>
        /// 不正値かどうか。
        /// </summary>
        bool IsInvalidValue { get; }

        /// <summary>
        /// 不正値チェック。不正値の場合は例外を投げる。
        /// </summary>
        void Validate();
    }
}
