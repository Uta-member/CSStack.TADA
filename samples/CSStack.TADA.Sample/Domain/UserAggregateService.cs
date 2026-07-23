namespace CSStack.TADA.Sample
{
    /// <summary>
    /// ユーザー集約の操作をまとめたサービス。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="AggregateServiceBase{TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession}"/>
    /// が実装してくれるのは <c>GetEntityByIdentifierAsync</c>（リポジトリへの委譲）だけ。
    /// 集約が持つべきルールはこのクラスに書く。
    /// </para>
    /// <para>
    /// <b>「存在しないのはエラーか」を決めるのはこの層。</b> リポジトリは不在を
    /// <c>Optional&lt;User&gt;.Empty</c> で返すだけで <see cref="ObjectNotFoundException"/> を投げない。
    /// 「存在しなければ失敗」という操作なのかどうかは、操作する側にしか分からないため。
    /// </para>
    /// <para>
    /// この層はトランザクションを開始しない。セッションは引数で受け取る。
    /// </para>
    /// </remarks>
    public sealed class UserAggregateService
        : AggregateServiceBase<User, UserId, IUserRepository, OperateInfo, AppSession>
    {
        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public UserAggregateService(IUserRepository repository)
            : base(repository)
        {
        }

        /// <summary>
        /// ユーザーを削除する。存在しなければ <see cref="ObjectNotFoundException"/>。
        /// </summary>
        public async ValueTask DeleteAsync(
            AppSession session,
            UserId identifier,
            OperateInfo operateInfo,
            CancellationToken cancellationToken = default)
        {
            var user = await GetRequiredAsync(session, identifier, cancellationToken);
            await Repository.DeleteAsync(session, user, operateInfo, cancellationToken);
        }

        /// <summary>
        /// ユーザーを取得する。存在しなければ <see cref="ObjectNotFoundException"/> を投げる。
        /// </summary>
        /// <remarks>
        /// 不在を異常とみなすのはこのメソッドの都合であって、リポジトリの都合ではない。
        /// 「居なくてもよい」場合は基底クラスの <c>GetEntityByIdentifierAsync</c> をそのまま使う。
        /// </remarks>
        /// <exception cref="ObjectNotFoundException">該当するユーザーが存在しない。</exception>
        public async ValueTask<User> GetRequiredAsync(
            AppSession session,
            UserId identifier,
            CancellationToken cancellationToken = default)
        {
            var found = await GetEntityByIdentifierAsync(session, identifier, cancellationToken);

            // Optional<T> から取り出すときは TryGetValue / Match を使う。
            if (!found.TryGetValue(out var user))
            {
                throw new ObjectNotFoundException(typeof(User), identifier);
            }

            return user;
        }

        /// <summary>
        /// ユーザーを新規登録する。同じ識別子が既に居れば <see cref="ObjectAlreadyExistException"/>。
        /// </summary>
        /// <remarks>
        /// <c>SaveAsync</c> は upsert なので、リポジトリに任せると黙って上書きになる。
        /// 「既に居たら失敗」はこの層で先に読んで判断する。
        /// </remarks>
        /// <exception cref="ObjectAlreadyExistException">同じ識別子のユーザーが既に存在する。</exception>
        public async ValueTask RegisterAsync(
            AppSession session,
            User user,
            OperateInfo operateInfo,
            CancellationToken cancellationToken = default)
        {
            var existing = await GetEntityByIdentifierAsync(session, user.Identifier, cancellationToken);
            if (existing.HasValue)
            {
                throw new ObjectAlreadyExistException(typeof(User), user.Identifier);
            }

            await Repository.SaveAsync(session, user, operateInfo, cancellationToken);
        }

        /// <summary>
        /// ユーザーの変更を保存する。
        /// </summary>
        public ValueTask SaveAsync(
            AppSession session,
            User user,
            OperateInfo operateInfo,
            CancellationToken cancellationToken = default)
        {
            return Repository.SaveAsync(session, user, operateInfo, cancellationToken);
        }
    }
}
