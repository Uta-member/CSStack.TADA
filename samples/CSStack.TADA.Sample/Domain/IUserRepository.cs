namespace CSStack.TADA.Sample
{
    /// <summary>
    /// ユーザー集約のリポジトリ。
    /// </summary>
    /// <typeparam name="TSession">
    /// トランザクションセッション型。<b>ここで具体的な型を書かない。</b>
    /// 集約は 1 つのリポジトリしか持たないので、名前は素の <c>TSession</c> でよい。
    /// </typeparam>
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
    /// <b>ここに <c>AppSession</c> と書いてはいけない。</b> インフラ層の実トランザクション因子を
    /// ドメイン層のインターフェースに焼き込むと、コンパイルは通るが TADA と DDD の利点が消える
    /// （ドメインが特定のデータストア実装に依存し、差し替えもテストも効かなくなる）。
    /// セッション型は型引数として外から受け取り、<b>実際の型が決まるのはプレゼンテーション層</b>
    /// — ユースケースとリポジトリを結びつける瞬間、つまり DI 登録のところだけ。
    /// 具体型を名指しするのは、この口を実装するインフラ層
    /// （<see cref="InMemoryUserRepository"/>）だけである。
    /// </para>
    /// </remarks>
    public interface IUserRepository<TSession> : IRepositoryDeletable<User, UserId, OperateInfo, TSession>
        where TSession : IDisposable
    {
    }
}
