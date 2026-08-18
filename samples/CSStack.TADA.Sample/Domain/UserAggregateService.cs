namespace CSStack.TADA.Sample
{
    /// <summary>
    /// <see cref="IUserAggregateService{TSession}"/> の実装。
    /// </summary>
    /// <typeparam name="TSession">
    /// トランザクションセッション型。扱うリポジトリは <see cref="IUserRepository{TSession}"/> 1 つだけなので、
    /// ここまでは素の <c>TSession</c> という名前で外から受け取ってよい。
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// <see cref="AggregateServiceBase{TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession}"/>
    /// が実装してくれるのは <c>GetEntityByIdentifierAsync</c>（リポジトリへの委譲）だけ。
    /// 集約が持つべきルールはこのクラスに書く。
    /// </para>
    /// <para>
    /// <b>ユースケースが注入するのはこのクラスではなく <see cref="IUserAggregateService{TSession}"/>。</b>
    /// 基底クラスを継承した具象クラスを直接注入すると、ユースケースのテストで
    /// この具象クラスとリポジトリ実装を組み立てる必要が出てしまう。
    /// </para>
    /// <para>
    /// <b>「存在しないのはエラーか」を決めるのはこの層。</b> リポジトリは不在を
    /// <c>Optional&lt;User&gt;.Empty</c> で返すだけで <see cref="UserNotFoundException"/> を投げない。
    /// 「存在しなければ失敗」という操作なのかどうかは、操作する側にしか分からないため。
    /// </para>
    /// <para>
    /// この層はトランザクションを開始しない。セッションは引数で受け取る。
    /// </para>
    /// </remarks>
    public sealed class UserAggregateService<TSession>
        : AggregateServiceBase<User, UserId, IUserRepository<TSession>, OperateInfo, TSession>,
        IUserAggregateService<TSession>
        where TSession : IDisposable
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public UserAggregateService(IUserRepository<TSession> repository)
            : base(repository)
        {
        }

        /// <inheritdoc/>
        public async ValueTask DeleteAsync(
            TSession session,
            UserId identifier,
            OperateInfo operateInfo,
            CancellationToken cancellationToken = default)
        {
            var user = await GetRequiredAsync(session, identifier, cancellationToken);
            await Repository.DeleteAsync(session, user, operateInfo, cancellationToken);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <c>Repository.SaveAsync</c> は upsert なので、リポジトリに任せると黙って上書きになる。
        /// 「既に居たら失敗」はこの層で先に読んで判断する。
        /// </remarks>
        public async ValueTask RegisterAsync(
            TSession session,
            User user,
            OperateInfo operateInfo,
            CancellationToken cancellationToken = default)
        {
            var existing = await GetEntityByIdentifierAsync(session, user.Identifier, cancellationToken);
            if (existing.HasValue)
            {
                throw new UserAlreadyExistsException($"ユーザー '{user.Identifier}' は既に存在します。");
            }

            await Repository.SaveAsync(session, user, operateInfo, cancellationToken);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// <b>リポジトリの <c>SaveAsync</c> を呼ぶのはこの層まで。</b> 上の層に見せるのは
        /// 「改名する」という操作だけで、そのために読み込みと保存が要ることは外から見えない。
        /// </remarks>
        public async ValueTask RenameAsync(
            TSession session,
            UserId identifier,
            UserName newName,
            OperateInfo operateInfo,
            CancellationToken cancellationToken = default)
        {
            var user = await GetRequiredAsync(session, identifier, cancellationToken);

            // 1 人の中で完結するルール（利用停止中は改名不可）はエンティティが持っている。
            user.Rename(newName);

            await Repository.SaveAsync(session, user, operateInfo, cancellationToken);
        }

        /// <summary>
        /// ユーザーを取得する。存在しなければ <see cref="UserNotFoundException"/> を投げる。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 不在を異常とみなすのはこのメソッドの都合であって、リポジトリの都合ではない。
        /// 「居なくてもよい」場合は基底クラスの <c>GetEntityByIdentifierAsync</c> をそのまま使う。
        /// </para>
        /// <para>
        /// <b>これは <see cref="IUserAggregateService{TSession}"/> に載せていない。</b>
        /// エンティティを上の層へ返すと、そこで書き換えられても保存する手段が集約の口に無く、
        /// 「変更したつもりが何も起きない」コードが書けてしまう。
        /// 読み取り目的なら <see cref="IQueryService{TRes}"/> の仕事。
        /// </para>
        /// </remarks>
        /// <exception cref="UserNotFoundException">該当するユーザーが存在しない。</exception>
        private async ValueTask<User> GetRequiredAsync(
            TSession session,
            UserId identifier,
            CancellationToken cancellationToken)
        {
            var found = await GetEntityByIdentifierAsync(session, identifier, cancellationToken);

            // Optional<T> から取り出すときは TryGetValue / Match を使う。
            if (!found.TryGetValue(out var user))
            {
                throw new UserNotFoundException(identifier.Value);
            }

            return user;
        }
    }
}
