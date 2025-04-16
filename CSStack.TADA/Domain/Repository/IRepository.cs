namespace CSStack.TADA
{
    /// <summary>
    /// リポジトリのインターフェース
    /// </summary>
    /// <typeparam name="TEntity">エンティティの型</typeparam>
    /// <typeparam name="TEntityIdentifier">エンティティの識別子となるオブジェクトの型</typeparam>
    /// <typeparam name="TOperateInfo">操作情報の型</typeparam>
    /// <typeparam name="TSession">トランザクションの因子の型</typeparam>
    public interface IRepository<TEntity, TEntityIdentifier, TOperateInfo, TSession>
        where TEntity : IEntity<TEntityIdentifier>
        where TSession : IDisposable
        where TEntityIdentifier : notnull
    {
        /// <summary>
        /// 識別子からエンティティを取得する。
        /// </summary>
        /// <param name="session">トランザクションの因子</param>
        /// <param name="identifier">エンティティの識別子</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>エンティティ</returns>
        ValueTask<Optional<TEntity>> FindByIdentifierAsync(
            TSession session,
            TEntityIdentifier identifier,
            CancellationToken cancellationToken);

        /// <summary>
        /// エンティティを永続化する
        /// </summary>
        /// <param name="session">トランザクションの因子</param>
        /// <param name="entity">エンティティ</param>
        /// <param name="operateInfo">操作情報</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        ValueTask SaveAsync(
            TSession session,
            TEntity entity,
            TOperateInfo operateInfo,
            CancellationToken cancellationToken);
    }
}
