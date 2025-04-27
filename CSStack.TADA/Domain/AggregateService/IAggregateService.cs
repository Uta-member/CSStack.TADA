namespace CSStack.TADA
{
    /// <summary>
    /// 集約サービスのインターフェース
    /// </summary>
    /// <typeparam name="TEntity">エンティティ</typeparam>
    /// <typeparam name="TEntityIdentifier">エンティティの識別子となるオブジェクトの型</typeparam>
    /// <typeparam name="TRepository">リポジトリ</typeparam>
    /// <typeparam name="TOperateInfo">操作情報の型</typeparam>
    /// <typeparam name="TSession">トランザクション因子の型</typeparam>
    public interface IAggregateService<TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession>
        where TEntity : IEntity<TEntityIdentifier>
        where TRepository : IRepository<TEntity, TEntityIdentifier, TOperateInfo, TSession>
        where TEntityIdentifier : notnull
        where TSession : IDisposable
    {
        /// <summary>
        /// エンティティを取得する
        /// </summary>
        /// <param name="session"></param>
        /// <param name="identifier"></param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns></returns>
        ValueTask<Optional<TEntity>> GetEntityByIdentifierAsync(
            TSession session,
            TEntityIdentifier identifier,
            CancellationToken cancellationToken = default);
    }
}
