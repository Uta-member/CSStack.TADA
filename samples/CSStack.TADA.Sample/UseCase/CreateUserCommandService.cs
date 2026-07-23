namespace CSStack.TADA.Sample
{
    /// <summary>
    /// <see cref="CreateUserCommandService"/> のリクエスト。
    /// </summary>
    /// <remarks>
    /// アプリケーションの境界に立つ DTO なので、素の値と操作情報だけを持つ。
    /// エンティティもセッションも持たない（セッションはコマンドサービス自身が始める）。
    /// </remarks>
    public sealed record CreateUserReq(string UserName, OperateInfo OperateInfo) : ICommandServiceDTO;

    /// <summary>
    /// <see cref="CreateUserCommandService"/> のレスポンス。採番した識別子を返す。
    /// </summary>
    /// <remarks>
    /// エンティティを返さないこと。エンティティは読み出したトランザクションのものであり、
    /// 呼び出し側に届く頃にはセッションは Dispose 済み。
    /// </remarks>
    public sealed record CreateUserRes(Guid UserId) : ICommandServiceDTO;

    /// <summary>
    /// ユーザーを登録するユースケース。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ここがトランザクションの境界。</b> <see cref="ITransactionManager"/> を注入し、
    /// <c>ExecuteTransactionAsync</c> で処理全体を包み、取り出したセッションを下の層へ渡す。
    /// これより下の層はトランザクションを開始しない。
    /// </para>
    /// <para>
    /// 本体が例外を投げれば自動的にロールバックされ、最後まで通ればコミットされる。
    /// </para>
    /// </remarks>
    public sealed class CreateUserCommandService : ICommandService<CreateUserReq, CreateUserRes>
    {
        private readonly ITransactionManager _transactionManager;
        private readonly UserAggregateService _userAggregateService;
        private readonly UserNameUniquenessService _userNameUniquenessService;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public CreateUserCommandService(
            ITransactionManager transactionManager,
            UserAggregateService userAggregateService,
            UserNameUniquenessService userNameUniquenessService)
        {
            _transactionManager = transactionManager;
            _userAggregateService = userAggregateService;
            _userNameUniquenessService = userNameUniquenessService;
        }

        /// <summary>
        /// 実行する。
        /// </summary>
        public async ValueTask<CreateUserRes> ExecuteAsync(
            CreateUserReq req,
            CancellationToken cancellationToken = default)
        {
            var userId = UserId.New();

            await _transactionManager.ExecuteTransactionAsync<AppSession>(
                async (sessions, token) =>
                {
                    var session = sessions.GetSession<AppSession>();

                    // 外部からの入力を値オブジェクトに変換する。検証は Create の中で行われ、
                    // 不正なら ValueObjectInvalidException 系が飛ぶ。
                    var userName = UserName.Create(req.UserName);

                    // 集約をまたぐルールはドメインサービスへ。セッションは DTO で渡す。
                    await _userNameUniquenessService.ExecuteAsync(
                        new EnsureUserNameIsUniqueReq(session, userName, userId),
                        token);

                    var user = User.Create(userId, userName);
                    await _userAggregateService.RegisterAsync(session, user, req.OperateInfo, token);
                },
                cancellationToken: cancellationToken);

            return new CreateUserRes(userId.Value);
        }
    }
}
