namespace CSStack.TADA.Sample
{
    /// <summary>
    /// ユーザー集約のリポジトリ。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 型引数は 4 つ。エンティティ / 識別子 / 操作情報 / セッション。
    /// <see cref="IRepositoryDeletable{TEntity, TEntityIdentifier, TOperateInfo, TSession}"/> を選ぶと
    /// <c>DeleteAsync</c> が加わる。削除しない集約なら <c>IRepository</c> のままでよい。
    /// </para>
    /// <para>
    /// <b>ここに検索系メソッドを足さない。</b> 使えるのは <c>FindByIdentifierAsync</c> と
    /// <c>SaveAsync</c>（upsert）と <c>DeleteAsync</c> だけ。一覧・条件検索は
    /// <see cref="IQueryService{TReq, TRes}"/> の仕事で、そちらはストアを直接読む。
    /// </para>
    /// <para>
    /// セッション型 <see cref="AppSession"/> がドメイン層から見えているのは TADA の設計上の帰結で、
    /// 事故ではない。詳細は docs/architecture.md を参照。
    /// </para>
    /// </remarks>
    public interface IUserRepository : IRepositoryDeletable<User, UserId, OperateInfo, AppSession>
    {
    }
}
