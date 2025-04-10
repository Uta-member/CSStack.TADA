namespace CSStack.TADA
{
    /// <summary>
    /// 集約サービスの基底クラス
    /// </summary>
    /// <typeparam name="TEntity">エンティティ</typeparam>
    /// <typeparam name="TEntityIdentifier">エンティティの識別子となるオブジェクトの型</typeparam>
    /// <typeparam name="TRepository">リポジトリ</typeparam>
    /// <typeparam name="TOperateInfo">操作情報の型</typeparam>
    /// <typeparam name="TSession">トランザクション因子の型</typeparam>
    public class AggregateServiceBase<TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession>
        : IAggregateService<TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession>
        where TEntity : IEntity<TEntityIdentifier>
        where TRepository : IRepository<TEntity, TEntityIdentifier, TOperateInfo, TSession>
        where TEntityIdentifier : notnull
        where TSession : IDisposable
    {
        /// <summary>
        /// リポジトリ
        /// </summary>
        protected readonly TRepository Repository;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="repository"></param>
        public AggregateServiceBase(TRepository repository)
        {
            Repository = repository;
        }

        /// <inheritdoc/>
        public async ValueTask<Optional<TEntity>> GetEntityByIdentifierAsync(
            TSession session,
            TEntityIdentifier identifier)
        {
            return await Repository.FindByIdentifierAsync(session, identifier);
        }
    }
}
