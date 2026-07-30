namespace CSStack.TADA.Sample
{
    /// <summary>
    /// 「その名前が他のユーザーに使われていないか」を調べる口。実装はインフラ層。
    /// </summary>
    /// <typeparam name="TSession">
    /// トランザクションセッション型。ユーザーが載っているストアのセッション。
    /// リポジトリと同じく、ここで具体的な型を書かない。
    /// </typeparam>
    /// <remarks>
    /// 一意性の判定は 1 人のユーザーの中では完結しないので、<see cref="IUserRepository{TSession}"/> には置けない
    /// （リポジトリに検索系メソッドを足さない、という規約でもある）。
    /// </remarks>
    public interface IUserNameDirectory<TSession>
        where TSession : IDisposable
    {
        /// <summary>
        /// <paramref name="name"/> が <paramref name="exceptUserId"/> 以外のユーザーに使われているか。
        /// </summary>
        ValueTask<bool> IsUsedByOtherAsync(
            TSession session,
            UserName name,
            UserId exceptUserId,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// ユーザー名の一意性を保証するドメインサービスの口。
    /// </summary>
    /// <typeparam name="TUserSession">
    /// ユーザー集約が載っているストアのセッション型。
    /// <b>集約をまたぐ層では <c>TSession</c> ではなく <c>T[集約名]Session</c> と名付ける。</b>
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// <b>ドメインサービスにも専用の口を立て、リクエストをその中にネストする。</b>
    /// <see cref="IDomainService{TReq}"/> は DTO の型で一意に定まるので、
    /// 注入するだけなら <c>IDomainService&lt;EnsureUserNameIsUniqueReq&lt;TUserSession&gt;&gt;</c> でも動く。
    /// ただしそれだと「このドメインサービスに何を渡すのか」を名前空間に平らに並んだ DTO から
    /// 探すことになり、口と DTO の対応がコンパイラにも読み手にも保証されない。
    /// <c>IUserNameUniquenessService&lt;TUserSession&gt;.Req</c> と口の中に置けば、
    /// 口から必ず辿れて、対応も 1 対 1 に固定される。
    /// </para>
    /// <para>
    /// <b>ドメインサービスはトランザクションを開始しない。</b>
    /// <see cref="ITransactionManager"/> を注入してはいけない。既に始まっているトランザクションの中で動く。
    /// </para>
    /// </remarks>
    public interface IUserNameUniquenessService<TUserSession>
        : IDomainService<IUserNameUniquenessService<TUserSession>.Req>
        where TUserSession : IDisposable
    {
        /// <summary>
        /// リクエスト。
        /// </summary>
        /// <remarks>
        /// <b>ドメインサービスの DTO はセッションを載せる。</b>
        /// <see cref="IDomainService{TReq}.ExecuteAsync"/> にセッション引数が無いため、
        /// 実行中のトランザクションを伝える経路がこれしかない。
        /// コマンドサービスやクエリサービスの DTO とは違い、エンティティや値オブジェクトを持ってよい。
        /// セッションを載せるとはいえ具体型は名指しせず、口の型引数をそのまま使う点は同じ。
        /// </remarks>
        sealed record Req(TUserSession Session, UserName Name, UserId ExceptUserId) : IDomainServiceDTO;
    }

    /// <summary>
    /// <see cref="IUserNameUniquenessService{TUserSession}"/> の実装。
    /// </summary>
    /// <typeparam name="TUserSession">
    /// ユーザー集約が載っているストアのセッション型。
    /// <b>集約をまたぐ層では <c>TSession</c> ではなく <c>T[集約名]Session</c> と名付ける。</b>
    /// ドメインサービスは複数の集約を触りうるため、後から別の集約
    /// （たとえば <c>TOrderSession</c>）が加わっても名前が衝突しないようにしておく。
    /// 集約ごとに別のデータストアであれば、セッション型も別になる。
    /// </typeparam>
    /// <remarks>
    /// 集約をまたぐルールなのでドメインサービスに置く。1 人の中で完結するルール
    /// （利用停止中は改名できない、など）は <see cref="User"/> 自身に置く。
    /// </remarks>
    public sealed class UserNameUniquenessService<TUserSession> : IUserNameUniquenessService<TUserSession>
        where TUserSession : IDisposable
    {
        private readonly IUserNameDirectory<TUserSession> _directory;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public UserNameUniquenessService(IUserNameDirectory<TUserSession> directory)
        {
            _directory = directory;
        }

        /// <summary>
        /// 名前が使われていれば <see cref="ObjectAlreadyExistException"/> を投げる。
        /// </summary>
        /// <exception cref="ObjectAlreadyExistException">その名前は既に他のユーザーが使っている。</exception>
        public async ValueTask ExecuteAsync(
            IUserNameUniquenessService<TUserSession>.Req req,
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
