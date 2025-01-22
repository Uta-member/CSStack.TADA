namespace CSStack.TADA
{
    /// <summary>
    /// オブジェクトが存在していない例外
    /// </summary>
    public class ObjectNotFoundException : TADAException
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="innserException">内部例外</param>
        public ObjectNotFoundException(string? message = null, Exception? innserException = null)
            : base(message, innserException)
        {
        }
    }
}
