using System.Collections.Immutable;

namespace CSStack.TADA
{
    /// <summary>
    /// 複数例外を同時に扱うための例外
    /// </summary>
    public class MultiReasonException : Exception
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="innerExceptions"></param>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public MultiReasonException(
            ImmutableList<Exception> innerExceptions,
            string? message = null,
            Exception? innerException = null)
            : base(message, innerException)
        {
            InnerExceptions = innerExceptions;
        }

        /// <summary>
        /// 例外を追加する
        /// </summary>
        /// <param name="exception">追加する例外</param>
        public void AddException(Exception exception)
        {
            InnerExceptions = InnerExceptions.Add(exception);
        }

        /// <summary>
        /// 同時発生した例外リスト
        /// </summary>
        public ImmutableList<Exception> InnerExceptions { get; protected set; }
    }
}
