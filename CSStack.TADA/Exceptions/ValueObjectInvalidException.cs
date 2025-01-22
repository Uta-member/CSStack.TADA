namespace CSStack.TADA
{
    /// <summary>
    /// 値オブジェクトの不正値例外クラス
    /// </summary>
    public class ValueObjectInvalidException : TADAException
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="innserException">内部例外</param>
        public ValueObjectInvalidException(string? message = null, Exception? innserException = null)
            : base(message, innserException)
        {
        }
    }
}
