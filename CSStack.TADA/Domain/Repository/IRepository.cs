using Microsoft.CodeAnalysis;

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
        /// 識別子から集約ルートを取得する。
        /// </summary>
        /// <param name="session">トランザクションの因子</param>
        /// <param name="identifier">集約ルートの識別子</param>
        /// <returns>集約ルート</returns>
        ValueTask<Optional<TEntity>> FindByIdentifierAsync(TSession session, TEntityIdentifier identifier);

        /// <summary>
        /// 集約を永続化する
        /// </summary>
        /// <param name="session">トランザクションの因子</param>
        /// <param name="aggregateRoot">集約ルート</param>
        /// <param name="operateInfo">操作情報</param>
        ValueTask SaveAsync(TSession session, TEntity aggregateRoot, TOperateInfo operateInfo);
    }
}
