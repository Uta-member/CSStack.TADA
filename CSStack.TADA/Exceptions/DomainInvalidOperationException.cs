namespace CSStack.TADA
{
    /// <summary>
    /// ドメイン内で不正な操作を行った場合の例外
    /// </summary>
    public class DomainInvalidOperationException : TADAException
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="innserException">内部例外</param>
        public DomainInvalidOperationException(string? message = null, Exception? innserException = null)
            : base(message, innserException)
        {
        }
    }
}
