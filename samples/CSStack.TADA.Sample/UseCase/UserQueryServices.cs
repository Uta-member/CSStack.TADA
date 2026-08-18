namespace CSStack.TADA.Sample
{
    /// <summary>
    /// 一覧に出す 1 行分。エンティティではなく画面の都合に合わせた素のデータ。
    /// </summary>
    /// <remarks>
    /// これは <c>Req</c> / <c>Res</c> そのものではなく複数のレスポンスで共有する読み取りモデルなので、
    /// どれか 1 つの口の中に入れず外に置く。ネストするのは口と 1 対 1 に対応する型だけ。
    /// </remarks>
    public sealed record UserSummary(Guid UserId, string Name, bool IsSuspended);

    /// <summary>
    /// 全ユーザーを一覧するクエリサービスの口。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>型引数が 1 つの <see cref="IQueryService{TRes}"/> は「レスポンス」を表す。</b>
    /// <c>ICommandService&lt;T&gt;</c> やドメインサービスの口の 1 つ目がリクエストなのと逆なので、
    /// 引数があるときは迷わず <see cref="IQueryService{TReq, TRes}"/> を使うとよい。
    /// </para>
    /// <para>
    /// <b>クエリサービスにも専用の口を立てて <c>Res</c> をネストする。</b>
    /// クエリサービスはセッション型引数を持たないので、口を立てる理由はコマンドサービスとは違い、
    /// 「レスポンス型をこのクエリと 1 対 1 に固定し、口から辿れる場所に置く」ためだけにある。
    /// <c>IQueryService&lt;ListUsersRes&gt;</c> のまま注入すると、
    /// 同じ形のレスポンスを返す別のクエリと DI 上で衝突しうる。
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
    public interface IListUsersQueryService : IQueryService<IListUsersQueryService.Res>
    {
        /// <summary>
        /// レスポンス。
        /// </summary>
        sealed record Res(IReadOnlyList<UserSummary> Users) : IQueryServiceDTO;
    }

    /// <summary>
    /// 名前の前方一致でユーザーを探すクエリサービスの口。
    /// </summary>
    /// <remarks>
    /// リクエストがあるので型引数 2 つの <see cref="IQueryService{TReq, TRes}"/>。
    /// <c>Req</c> と <c>Res</c> の両方をここにネストする。
    /// </remarks>
    public interface ISearchUsersQueryService
        : IQueryService<ISearchUsersQueryService.Req, ISearchUsersQueryService.Res>
    {
        /// <summary>
        /// リクエスト。名前の前方一致で絞り込む。
        /// </summary>
        sealed record Req(string NamePrefix) : IQueryServiceDTO;

        /// <summary>
        /// レスポンス。
        /// </summary>
        sealed record Res(IReadOnlyList<UserSummary> Users) : IQueryServiceDTO;
    }

    /// <summary>
    /// <see cref="IListUsersQueryService"/> の実装。
    /// </summary>
    public sealed class ListUsersQueryService : IListUsersQueryService
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
        public ValueTask<IListUsersQueryService.Res> ExecuteAsync(
            CancellationToken cancellationToken = default)
        {
            var users = _database.Users
                .Select(row => new UserSummary(row.Id, row.Name, row.IsSuspended))
                .OrderBy(user => user.Name, StringComparer.Ordinal)
                .ToList();

            return ValueTask.FromResult(new IListUsersQueryService.Res(users));
        }
    }

    /// <summary>
    /// <see cref="ISearchUsersQueryService"/> の実装。
    /// </summary>
    public sealed class SearchUsersQueryService : ISearchUsersQueryService
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
        public ValueTask<ISearchUsersQueryService.Res> ExecuteAsync(
            ISearchUsersQueryService.Req req,
            CancellationToken cancellationToken = default)
        {
            var users = _database.Users
                .Where(row => row.Name.StartsWith(req.NamePrefix, StringComparison.Ordinal))
                .Select(row => new UserSummary(row.Id, row.Name, row.IsSuspended))
                .OrderBy(user => user.Name, StringComparer.Ordinal)
                .ToList();

            return ValueTask.FromResult(new ISearchUsersQueryService.Res(users));
        }
    }
}
