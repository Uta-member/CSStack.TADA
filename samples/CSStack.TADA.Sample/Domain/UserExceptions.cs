namespace CSStack.TADA.Sample
{
    /// <summary>
    /// ユーザー名の入力が不正だったときの例外。
    /// </summary>
    /// <remarks>
    /// TADA 自体はもう例外型を提供しない。値オブジェクトの検証で何を投げるかは、
    /// これを使うプロジェクト自身が決める（このファイルはその一例）。
    /// </remarks>
    public class UserNameInvalidException : Exception
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="message">メッセージ</param>
        public UserNameInvalidException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// ユーザー名の長さが範囲外だったときの例外。上下限と実際の長さを保持する。
    /// </summary>
    public sealed class UserNameLengthException : UserNameInvalidException
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="minLength">受け付ける最小長</param>
        /// <param name="maxLength">受け付ける最大長</param>
        /// <param name="currentLength">実際に渡された長さ</param>
        public UserNameLengthException(int minLength, int maxLength, int currentLength)
            : base($"ユーザー名は {minLength}〜{maxLength} 文字である必要があります(実際は {currentLength} 文字)。")
        {
            MinLength = minLength;
            MaxLength = maxLength;
            CurrentLength = currentLength;
        }

        /// <summary>
        /// 実際に渡された長さ。
        /// </summary>
        public int CurrentLength { get; }

        /// <summary>
        /// 受け付ける最大長。
        /// </summary>
        public int MaxLength { get; }

        /// <summary>
        /// 受け付ける最小長。
        /// </summary>
        public int MinLength { get; }
    }

    /// <summary>
    /// 利用停止中のユーザーに許されない操作を行おうとしたときの例外。
    /// </summary>
    public sealed class UserSuspendedException : Exception
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="message">メッセージ</param>
        public UserSuspendedException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// 既に存在するはずのないユーザーが見つかったときの例外。
    /// </summary>
    public sealed class UserAlreadyExistsException : Exception
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="message">メッセージ</param>
        public UserAlreadyExistsException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// 存在するはずのユーザーが見つからなかったときの例外。
    /// </summary>
    public sealed class UserNotFoundException : Exception
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        /// <param name="userId">見つからなかったユーザーの識別子</param>
        public UserNotFoundException(Guid userId)
            : base($"ユーザー '{userId}' が見つかりません。")
        {
            UserId = userId;
        }

        /// <summary>
        /// 見つからなかったユーザーの識別子。
        /// </summary>
        public Guid UserId { get; }
    }
}
