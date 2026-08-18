namespace CSStack.TADA.Sample
{
    /// <summary>
    /// ユーザーを登録するユースケースの口。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>セッション型引数を持たないインターフェースを立てるのが要点。</b>
    /// 実装 <see cref="CreateUserCommandService{TUserSession}"/> はセッション型を型引数に持つが、
    /// プレゼンテーション層はこの口だけを見るので <see cref="AppSession"/> を書かずに呼び出せる。
    /// 型引数を書くのは DI 登録の 1 行だけになる（→ <see cref="Program"/>）。
    /// </para>
    /// <para>
    /// <b>リクエストとレスポンスはこの中にネストして <c>Req</c> / <c>Res</c> と名付ける。</b>
    /// <see cref="ICommandService{TReq, TRes}"/> を継承した時点で「メソッドは 1 つ、
    /// リクエストとレスポンスの型がそれぞれ 1 つ」が確定しているので、
    /// その 2 つは口と 1 対 1 に対応する。外に <c>CreateUserReq</c> として置くと、
    /// 名前空間に平らに並んだ DTO 群からこの口に対応するものを名前で探すことになり、
    /// 別のユースケースのリクエストを渡す事故（型が合えば通ってしまう）も起こりうる。
    /// ネストしておけば <c>ICreateUserCommandService.Req</c> と、口から辿れる位置に必ずある。
    /// </para>
    /// <para>
    /// テストのときも、このインターフェースを差し替えるだけで済む。
    /// </para>
    /// </remarks>
    public interface ICreateUserCommandService
        : ICommandService<ICreateUserCommandService.Req, ICreateUserCommandService.Res>
    {
        /// <summary>
        /// リクエスト。
        /// </summary>
        /// <remarks>
        /// アプリケーションの境界に立つ DTO なので、素の値と操作情報だけを持つ。
        /// エンティティもセッションも持たない（セッションはコマンドサービス自身が始める）。
        /// <b>したがってこの DTO はセッション型引数を取らない</b> — 呼び出し側にセッションの存在を
        /// 知らせないための境界であり、ここに型引数が現れたら設計が漏れている。
        /// </remarks>
        sealed record Req(string UserName, OperateInfo OperateInfo) : ICommandServiceDTO;

        /// <summary>
        /// レスポンス。採番した識別子を返す。
        /// </summary>
        /// <remarks>
        /// エンティティを返さないこと。エンティティは読み出したトランザクションのものであり、
        /// 呼び出し側に届く頃にはセッションは Dispose 済み。
        /// </remarks>
        sealed record Res(Guid UserId) : ICommandServiceDTO;
    }

    /// <summary>
    /// <see cref="ICreateUserCommandService"/> の実装。
    /// </summary>
    /// <typeparam name="TUserSession">
    /// ユーザー集約が載っているストアのセッション型。
    /// <b>ユースケースで <c>TSession</c> という名前を使わない。</b>
    /// 今は 1 集約しか触っていないが、ユースケースは複数の集約を跨ぐのが普通で、
    /// 集約ごとにデータストアが違えばセッション型も違う。そのとき
    /// <c>ExecuteTransactionAsync&lt;TUserSession, TOrderSession&gt;</c> のように並べられる名前にしておく。
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// <b>ここがトランザクションの境界。</b> <see cref="ITransactionManager"/> を注入し、
    /// <c>ExecuteTransactionAsync</c> で処理全体を包み、取り出したセッションを下の層へ渡す。
    /// これより下の層はトランザクションを開始しない。
    /// </para>
    /// <para>
    /// 本体が例外を投げれば自動的にロールバックされ、最後まで通ればコミットされる。
    /// </para>
    /// <para>
    /// <b>注入するのは下の層のインターフェース。</b> 集約サービスは
    /// <see cref="IUserAggregateService{TSession}"/>、ドメインサービスは
    /// <see cref="IUserNameUniquenessService{TUserSession}"/> で受ける。具象クラスを直接受け取ると、
    /// このユースケースのテストがリポジトリ実装まで組み立てる話になる。
    /// </para>
    /// <para>
    /// <b>セッションの具体型はこのクラスでも決まらない。</b> <typeparamref name="TUserSession"/> が
    /// 何になるかが決まるのは、このユースケースとリポジトリ実装を結びつける瞬間
    /// — <see cref="Program"/> の DI 登録、すなわちプレゼンテーション層である。
    /// </para>
    /// </remarks>
    public sealed class CreateUserCommandService<TUserSession> : ICreateUserCommandService
        where TUserSession : IDisposable
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IUserAggregateService<TUserSession> _userAggregateService;
        private readonly IUserNameUniquenessService<TUserSession> _userNameUniquenessService;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public CreateUserCommandService(
            ITransactionManager transactionManager,
            IUserAggregateService<TUserSession> userAggregateService,
            IUserNameUniquenessService<TUserSession> userNameUniquenessService)
        {
            _transactionManager = transactionManager;
            _userAggregateService = userAggregateService;
            _userNameUniquenessService = userNameUniquenessService;
        }

        /// <summary>
        /// 実行する。
        /// </summary>
        public async ValueTask<ICreateUserCommandService.Res> ExecuteAsync(
            ICreateUserCommandService.Req req,
            CancellationToken cancellationToken = default)
        {
            var userId = UserId.New();

            // 集約が増えたら ExecuteTransactionAsync<TUserSession, TOrderSession> と並べる。
            // 型引数は具体型ではなく、このクラスが外から受け取った型引数をそのまま渡す。
            await _transactionManager.ExecuteTransactionAsync<TUserSession>(
                async (sessions, token) =>
                {
                    var session = sessions.GetSession<TUserSession>();

                    // 外部からの入力を値オブジェクトに変換する。検証は Create の中で行われ、
                    // 不正なら UserNameInvalidException 系が飛ぶ。
                    var userName = UserName.Create(req.UserName);

                    // 集約をまたぐルールはドメインサービスへ。セッションは DTO で渡す。
                    await _userNameUniquenessService.ExecuteAsync(
                        new IUserNameUniquenessService<TUserSession>.Req(session, userName, userId),
                        token);

                    var user = User.Create(userId, userName);
                    await _userAggregateService.RegisterAsync(session, user, req.OperateInfo, token);
                },
                cancellationToken: cancellationToken);

            return new ICreateUserCommandService.Res(userId.Value);
        }
    }
}
