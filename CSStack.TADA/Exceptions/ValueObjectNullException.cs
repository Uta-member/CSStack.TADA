namespace CSStack.TADA
{
    /// <summary>
    /// 値オブジェクトに値が入っていない例外
    /// </summary>
    public class ValueObjectNullException : ValueObjectInvalidException
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="innserException">内部例外</param>
        public ValueObjectNullException(string? message = null, Exception? innserException = null)
            : base(message, innserException)
        {
        }
    }
}
