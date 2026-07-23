namespace CSStack.TADA.Sample
{
    /// <summary>
    /// 「その名前が他のユーザーに使われていないか」を調べる口。実装はインフラ層。
    /// </summary>
    /// <remarks>
    /// 一意性の判定は 1 人のユーザーの中では完結しないので、<see cref="IUserRepository"/> には置けない
    /// （リポジトリに検索系メソッドを足さない、という規約でもある）。
    /// </remarks>
    public interface IUserNameDirectory
    {
        /// <summary>
        /// <paramref name="name"/> が <paramref name="exceptUserId"/> 以外のユーザーに使われているか。
        /// </summary>
        ValueTask<bool> IsUsedByOtherAsync(
            AppSession session,
            UserName name,
            UserId exceptUserId,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// <see cref="UserNameUniquenessService"/> のリクエスト。
    /// </summary>
    /// <remarks>
    /// <b>ドメインサービスの DTO はセッションを載せる。</b>
    /// <see cref="IDomainService{TReq}.ExecuteAsync"/> にセッション引数が無いため、
    /// 実行中のトランザクションを伝える経路がこれしかない。
    /// コマンドサービスやクエリサービスの DTO とは違い、エンティティや値オブジェクトを持ってよい。
    /// </remarks>
    public sealed record EnsureUserNameIsUniqueReq(AppSession Session, UserName Name, UserId ExceptUserId)
        : IDomainServiceDTO;

    /// <summary>
    /// ユーザー名の一意性を保証するドメインサービス。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 集約をまたぐルールなのでドメインサービスに置く。1 人の中で完結するルール
    /// （利用停止中は改名できない、など）は <see cref="User"/> 自身に置く。
    /// </para>
    /// <para>
    /// <b>ドメインサービスはトランザクションを開始しない。</b>
    /// <see cref="ITransactionManager"/> を注入してはいけない。既に始まっているトランザクションの中で動く。
    /// </para>
    /// </remarks>
    public sealed class UserNameUniquenessService : IDomainService<EnsureUserNameIsUniqueReq>
    {
        private readonly IUserNameDirectory _directory;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public UserNameUniquenessService(IUserNameDirectory directory)
        {
            _directory = directory;
        }

        /// <summary>
        /// 名前が使われていれば <see cref="ObjectAlreadyExistException"/> を投げる。
        /// </summary>
        /// <exception cref="ObjectAlreadyExistException">その名前は既に他のユーザーが使っている。</exception>
        public async ValueTask ExecuteAsync(
            EnsureUserNameIsUniqueReq req,
            CancellationToken cancellationToken = default)
        {
            var isUsed = await _directory.IsUsedByOtherAsync(
                req.Session,
                req.Name,
                req.ExceptUserId,
                cancellationToken);

            if (isUsed)
            {
                throw new ObjectAlreadyExistException(typeof(User), req.Name.Value);
            }
        }
    }
}
