namespace CSStack.TADA.Sample
{
    /// <summary>
    /// ユーザー集約の操作を宣言するインターフェース。
    /// </summary>
    /// <typeparam name="TSession">
    /// トランザクションセッション型。集約が扱うリポジトリは 1 つなので、素の <c>TSession</c> でよい。
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// <b>集約サービスはインターフェースを立ててから実装する。</b>
    /// <see cref="AggregateServiceBase{TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession}"/>
    /// を直接継承した具象クラスをユースケースに注入すると、ユースケースのテストのために
    /// 集約サービスの具象クラスを — さらにその先のリポジトリ実装まで — 必ず組み立てることになる。
    /// 口がインターフェースなら、ユースケースのテストはこれを差し替えるだけで済む。
    /// </para>
    /// <para>
    /// 基底の
    /// <see cref="IAggregateService{TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession}"/>
    /// が宣言するのは <c>GetEntityByIdentifierAsync</c>（リポジトリへの委譲）だけ。
    /// 集約の操作はこのインターフェースに足す。
    /// </para>
    /// <para>
    /// 型引数の 3 つ目が <see cref="IUserRepository{TSession}"/> になっているのは、
    /// 「1 集約にリポジトリは 1 つ」を型で表明する TADA の設計。
    /// </para>
    /// <para>
    /// <b><c>SaveAsync</c> のような汎用的な操作をここに置かない。</b> このインターフェースは
    /// 実質的に集約ルートであり、並んでいるメソッドが「このドメインに何ができるか」の一覧になる。
    /// <c>RegisterAsync</c> と <c>SaveAsync</c> が両方あれば、呼ぶ側はたいてい何でも通る
    /// <c>SaveAsync</c> を選び、<c>RegisterAsync</c> が持っていた「既に居たら失敗」という
    /// ルールは素通りされる。ルールの置き場所として分けた意味がなくなる。
    /// 「読み込む → 変更する → 保存する」を <c>RenameAsync</c> のような 1 つの操作として閉じれば、
    /// エンティティを外に出さずに済み、名前がそのままドメインの語彙になる。
    /// </para>
    /// </remarks>
    public interface IUserAggregateService<TSession>
        : IAggregateService<User, UserId, IUserRepository<TSession>, OperateInfo, TSession>
        where TSession : IDisposable
    {
        /// <summary>
        /// ユーザーを削除する。存在しなければ <see cref="ObjectNotFoundException"/>。
        /// </summary>
        /// <exception cref="ObjectNotFoundException">該当するユーザーが存在しない。</exception>
        ValueTask DeleteAsync(
            TSession session,
            UserId identifier,
            OperateInfo operateInfo,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// ユーザーを新規登録する。同じ識別子が既に居れば <see cref="ObjectAlreadyExistException"/>。
        /// </summary>
        /// <exception cref="ObjectAlreadyExistException">同じ識別子のユーザーが既に存在する。</exception>
        ValueTask RegisterAsync(
            TSession session,
            User user,
            OperateInfo operateInfo,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// ユーザーの名前を変更する。存在しなければ <see cref="ObjectNotFoundException"/>。
        /// </summary>
        /// <remarks>
        /// 「取得して、変更して、保存する」をこの 1 つの操作に閉じている。
        /// ユースケースにエンティティを渡して <c>SaveAsync</c> を呼ばせる形にすると、
        /// 保存を忘れても型では気づけず、集約の外でエンティティが書き換わる余地も残る。
        /// </remarks>
        /// <exception cref="ObjectNotFoundException">該当するユーザーが存在しない。</exception>
        /// <exception cref="DomainInvalidOperationException">利用停止中のユーザーだった。</exception>
        ValueTask RenameAsync(
            TSession session,
            UserId identifier,
            UserName newName,
            OperateInfo operateInfo,
            CancellationToken cancellationToken = default);
    }
}
