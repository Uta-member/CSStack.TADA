namespace CSStack.TADA
{
    /// <summary>
    /// TADAの例外クラス
    /// </summary>
    public class TADAException : Exception
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="innserException">内部例外</param>
        public TADAException(string? message = null, Exception? innserException = null)
            : base(message, innserException)
        {
        }
    }
}
