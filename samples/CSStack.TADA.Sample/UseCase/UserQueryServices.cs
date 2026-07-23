namespace CSStack.TADA.Sample
{
    /// <summary>
    /// 一覧に出す 1 行分。エンティティではなく画面の都合に合わせた素のデータ。
    /// </summary>
    public sealed record UserSummary(Guid UserId, string Name, bool IsSuspended);

    /// <summary>
    /// ユーザー一覧のレスポンス。
    /// </summary>
    public sealed record ListUsersRes(IReadOnlyList<UserSummary> Users) : IQueryServiceDTO;

    /// <summary>
    /// 名前で絞り込むリクエスト。
    /// </summary>
    public sealed record SearchUsersReq(string NamePrefix) : IQueryServiceDTO;

    /// <summary>
    /// 名前で絞り込んだ結果。
    /// </summary>
    public sealed record SearchUsersRes(IReadOnlyList<UserSummary> Users) : IQueryServiceDTO;

    /// <summary>
    /// 全ユーザーを一覧するクエリサービス。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>型引数が 1 つの <see cref="IQueryService{TRes}"/> は「レスポンス」を表す。</b>
    /// <c>ICommandService&lt;T&gt;</c> や <c>IDomainService&lt;T&gt;</c> の 1 つ目がリクエストなのと逆なので、
    /// 引数があるときは迷わず <see cref="IQueryService{TReq, TRes}"/> を使うとよい。
    /// </para>
    /// <para>
    /// <b>リポジトリを通さずストアを直接読む。</b> エンティティを組み立ててから一覧用に潰すのは
    /// 無駄な上に、集約の境界を画面側に引きずり出すことになる。
    /// リポジトリに <c>FindAllAsync</c> が無いのはこのため。
    /// </para>
    /// <para>
    /// 読み取りにトランザクションは要らないので <see cref="ITransactionManager"/> も注入しない。
    /// 実行中のトランザクションの未コミット状態を読む必要があるときだけ、
    /// リクエスト DTO にセッションを載せて渡す（2 つ目のトランザクションを開始しない）。
    /// </para>
    /// </remarks>
    public sealed class ListUsersQueryService : IQueryService<ListUsersRes>
    {
        private readonly AppDatabase _database;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public ListUsersQueryService(AppDatabase database)
        {
            _database = database;
        }

        /// <summary>
        /// 実行する。
        /// </summary>
        public ValueTask<ListUsersRes> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var users = _database.Users
                .Select(row => new UserSummary(row.Id, row.Name, row.IsSuspended))
                .OrderBy(user => user.Name, StringComparer.Ordinal)
                .ToList();

            return ValueTask.FromResult(new ListUsersRes(users));
        }
    }

    /// <summary>
    /// 名前の前方一致でユーザーを探すクエリサービス。
    /// </summary>
    /// <remarks>
    /// リクエストがあるので型引数 2 つの <see cref="IQueryService{TReq, TRes}"/>。
    /// </remarks>
    public sealed class SearchUsersQueryService : IQueryService<SearchUsersReq, SearchUsersRes>
    {
        private readonly AppDatabase _database;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public SearchUsersQueryService(AppDatabase database)
        {
            _database = database;
        }

        /// <summary>
        /// 実行する。
        /// </summary>
        public ValueTask<SearchUsersRes> ExecuteAsync(
            SearchUsersReq req,
            CancellationToken cancellationToken = default)
        {
            var users = _database.Users
                .Where(row => row.Name.StartsWith(req.NamePrefix, StringComparison.Ordinal))
                .Select(row => new UserSummary(row.Id, row.Name, row.IsSuspended))
                .OrderBy(user => user.Name, StringComparer.Ordinal)
                .ToList();

            return ValueTask.FromResult(new SearchUsersRes(users));
        }
    }
}
