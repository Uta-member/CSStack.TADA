namespace CSStack.TADA.Sample
{
    /// <summary>
    /// <see cref="RenameUserCommandService"/> のリクエスト。
    /// </summary>
    public sealed record RenameUserReq(Guid UserId, string NewName, OperateInfo OperateInfo) : ICommandServiceDTO;

    /// <summary>
    /// ユーザー名を変更するユースケース。
    /// </summary>
    /// <remarks>
    /// 「読む → 変更する → 保存する」がまるごと 1 つのトランザクションに入る。
    /// 途中のどこで例外が出てもロールバックされ、何も残らない。
    /// </remarks>
    public sealed class RenameUserCommandService : ICommandService<RenameUserReq>
    {
        private readonly ITransactionManager _transactionManager;
        private readonly UserAggregateService _userAggregateService;
        private readonly UserNameUniquenessService _userNameUniquenessService;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public RenameUserCommandService(
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
        public ValueTask ExecuteAsync(RenameUserReq req, CancellationToken cancellationToken = default)
        {
            return _transactionManager.ExecuteTransactionAsync<AppSession>(
                async (sessions, token) =>
                {
                    var session = sessions.GetSession<AppSession>();

                    var userId = UserId.Create(req.UserId);
                    var newName = UserName.Create(req.NewName);

                    // 存在しなければ ObjectNotFoundException。判断しているのは集約サービス。
                    var user = await _userAggregateService.GetRequiredAsync(session, userId, token);

                    await _userNameUniquenessService.ExecuteAsync(
                        new EnsureUserNameIsUniqueReq(session, newName, userId),
                        token);

                    // 1 人の中で完結するルール（利用停止中は改名不可）はエンティティが持っている。
                    user.Rename(newName);

                    await _userAggregateService.SaveAsync(session, user, req.OperateInfo, token);
                },
                cancellationToken: cancellationToken);
        }
    }
}
