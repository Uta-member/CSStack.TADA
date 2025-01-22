namespace CSStack.TADA
{
    /// <summary>
    /// すでにオブジェクトが存在している例外
    /// </summary>
    public class ObjectAlreadyExistException : TADAException
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="innserException">内部例外</param>
        public ObjectAlreadyExistException(string? message = null, Exception? innserException = null)
            : base(message, innserException)
        {
        }
    }
}
