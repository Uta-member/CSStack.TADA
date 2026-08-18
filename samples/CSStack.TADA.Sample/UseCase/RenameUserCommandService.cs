namespace CSStack.TADA.Sample
{
    /// <summary>
    /// ユーザー名を変更するユースケースの口。
    /// </summary>
    /// <remarks>
    /// <para>
    /// セッション型引数を持たないので、プレゼンテーション層はセッション型を知らずに呼び出せる。
    /// 型引数を書くのは DI 登録の 1 行だけ（→ <see cref="Program"/>）。
    /// </para>
    /// <para>
    /// レスポンスが要らないユースケースなので <see cref="ICommandService{TReq}"/> を継承し、
    /// ネストするのは <c>Req</c> だけ。返すものが無いなら <c>Res</c> を作らない。
    /// </para>
    /// </remarks>
    public interface IRenameUserCommandService : ICommandService<IRenameUserCommandService.Req>
    {
        /// <summary>
        /// リクエスト。
        /// </summary>
        /// <remarks>
        /// 境界の DTO なのでセッション型引数を取らない。<c>Guid</c> と <c>string</c> のまま受け取り、
        /// 値オブジェクトへの変換はトランザクションの中で行う。
        /// </remarks>
        sealed record Req(Guid UserId, string NewName, OperateInfo OperateInfo) : ICommandServiceDTO;
    }

    /// <summary>
    /// <see cref="IRenameUserCommandService"/> の実装。
    /// </summary>
    /// <typeparam name="TUserSession">
    /// ユーザー集約が載っているストアのセッション型。具体型はプレゼンテーション層の DI 登録で決まる。
    /// </typeparam>
    /// <remarks>
    /// 「読む → 変更する → 保存する」がまるごと 1 つのトランザクションに入る。
    /// 途中のどこで例外が出てもロールバックされ、何も残らない。
    /// </remarks>
    public sealed class RenameUserCommandService<TUserSession> : IRenameUserCommandService
        where TUserSession : IDisposable
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IUserAggregateService<TUserSession> _userAggregateService;
        private readonly IUserNameUniquenessService<TUserSession> _userNameUniquenessService;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public RenameUserCommandService(
            ITransactionManager transactionManager,
            IUserAggregateService<TUserSession> userAggregateService,
            IUserNameUniquenessService<TUserSession> userNameUniquenessService)
        {
            _transactionManager = transactionManager;
            _userAggregateService = userAggregateService;
            _userNameUniquenessService = userNameUniquenessService;
        }

        /// <summary>
        /// 実行する。
        /// </summary>
        public ValueTask ExecuteAsync(
            IRenameUserCommandService.Req req,
            CancellationToken cancellationToken = default)
        {
            return _transactionManager.ExecuteTransactionAsync<TUserSession>(
                async (sessions, token) =>
                {
                    var session = sessions.GetSession<TUserSession>();

                    var userId = UserId.Create(req.UserId);
                    var newName = UserName.Create(req.NewName);

                    // 集約をまたぐルールはドメインサービス。
                    await _userNameUniquenessService.ExecuteAsync(
                        new IUserNameUniquenessService<TUserSession>.Req(session, newName, userId),
                        token);

                    // 「読み込む → 変更する → 保存する」は集約サービスの中に閉じている。
                    // ここでエンティティを受け取って書き換えて保存する形にしないのが要点で、
                    // だから IUserAggregateService に SaveAsync は無い。
                    // 存在しなければ UserNotFoundException。判断しているのは集約サービス。
                    await _userAggregateService.RenameAsync(session, userId, newName, req.OperateInfo, token);
                },
                cancellationToken: cancellationToken);
        }
    }
}
