namespace CSStack.TADA
{
    /// <summary>
    /// 値オブジェクトの長さに関する例外
    /// </summary>
    public class ValueObjectLengthException : ValueObjectInvalidException
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="minLength">最小長</param>
        /// <param name="maxLength">最大長</param>
        /// <param name="currentLength">現在値</param>
        /// <param name="message">メッセージ</param>
        /// <param name="innserException">内部例外</param>
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
        /// 現在値
        /// </summary>
        public int CurrentLength { get; }

        /// <summary>
        /// 最大長
        /// </summary>
        public int MaxLength { get; }

        /// <summary>
        /// 最小長
        /// </summary>
        public int MinLength { get; }
    }
}
