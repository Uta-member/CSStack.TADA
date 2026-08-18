using Microsoft.Extensions.DependencyInjection;

namespace CSStack.TADA.Sample
{
    /// <summary>
    /// プレゼンテーション層に相当する。DI を組み立て、ユースケースを呼ぶだけ。
    /// トランザクションのことは知らない（境界はコマンドサービスにある）。
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// エントリポイント。
        /// </summary>
        public static async Task Main()
        {
            var provider = BuildServiceProvider();
            var operateInfo = OperateInfo.Now("sample-operator");

            // 1. 登録する（コミットされる）
            Console.WriteLine("== 1. ユーザーを登録する ==");
            var aliceId = await CreateUserAsync(provider, "alice", operateInfo);
            var bobId = await CreateUserAsync(provider, "bob", operateInfo);
            Console.WriteLine($"alice = {aliceId}");
            Console.WriteLine($"bob   = {bobId}");
            await PrintUsersAsync(provider);

            // 2. 名前を変更する（コミットされる）
            Console.WriteLine("== 2. bob を robert に改名する ==");
            await RenameUserAsync(provider, bobId, "robert", operateInfo);
            await PrintUsersAsync(provider);

            // 3. 重複する名前で失敗させる（ロールバックされ、何も残らない）
            Console.WriteLine("== 3. 既にある名前 'alice' で登録を試みる ==");
            try
            {
                await CreateUserAsync(provider, "alice", operateInfo);
            }
            catch (UserAlreadyExistsException exception)
            {
                Console.WriteLine($"想定どおり失敗: {exception.Message}");
            }

            Console.WriteLine("ロールバックされたので件数は変わらない:");
            await PrintUsersAsync(provider);

            // 4. 値オブジェクトの検証で弾く（トランザクションに入る前に落ちる）
            Console.WriteLine("== 4. 長すぎる名前で登録を試みる ==");
            try
            {
                await CreateUserAsync(provider, new string('x', 100), operateInfo);
            }
            catch (UserNameLengthException exception)
            {
                Console.WriteLine(
                    $"想定どおり失敗: 許容 {exception.MinLength}〜{exception.MaxLength} 文字, "
                    + $"実際 {exception.CurrentLength} 文字");
            }

            // 5. 存在しないユーザーを改名する
            Console.WriteLine("== 5. 存在しないユーザーを改名する ==");
            try
            {
                await RenameUserAsync(provider, Guid.NewGuid(), "nobody", operateInfo);
            }
            catch (UserNotFoundException exception)
            {
                Console.WriteLine($"想定どおり失敗: User / {exception.UserId}");
            }

            // 6. クエリサービスで絞り込む
            Console.WriteLine("== 6. 名前が 'a' で始まるユーザーを探す ==");
            using (var scope = provider.CreateScope())
            {
                var searchService = scope.ServiceProvider.GetRequiredService<ISearchUsersQueryService>();
                var found = await searchService.ExecuteAsync(new ISearchUsersQueryService.Req("a"));
                foreach (var user in found.Users)
                {
                    Console.WriteLine($"  - {user.Name}");
                }
            }

            // 7. セッションの所有権を確認する
            Console.WriteLine("== 7. セッションは TransactionManager が Dispose する ==");
            await ShowSessionOwnershipAsync(provider);
        }

        /// <summary>
        /// DI コンテナを組み立てる。<b>ここが最初につまずくところ。</b>
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>セッション型が確定するのはここだけ。</b> ドメイン層とユースケース層は
        /// セッション型を型引数（<c>TSession</c> / <c>TUserSession</c>）として外から受け取るだけで、
        /// <see cref="AppSession"/> という名前を一切知らない。
        /// ユースケースとリポジトリ実装を結びつけるこの瞬間 — つまりプレゼンテーション層の
        /// DI 登録（あるいは <c>new</c> でユースケースを組み立てる場所）で初めて型が決まる。
        /// </para>
        /// <para>
        /// 逆にこの構成の意味は、<c>IUserRepository&lt;AppSession&gt;</c> の登録先を差し替えれば
        /// ドメインもユースケースも一行も変えずにストアを取り替えられる、というところにある。
        /// もしドメイン層のインターフェースに <see cref="AppSession"/> と書いていたら、
        /// それが不可能になる（テストダブルも作れない）。
        /// </para>
        /// <para>
        /// <b>登録はすべて「インターフェース → 実装」。</b> 集約サービスもユースケースも
        /// 具象クラスを直接登録・注入しない。上の層が具象に依存すると、テストのために
        /// リポジトリ実装まで組み立てる必要が出てしまう。
        /// </para>
        /// </remarks>
        private static ServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();

            // データベース相当。アプリの寿命と同じなのでシングルトン。
            services.AddSingleton<AppDatabase>();

            // --- TADA の登録 -------------------------------------------------------------------
            //
            // TransactionManager は実行中のセッションを可変フィールドに保持し、スレッドセーフではない。
            // 必ず Scoped で登録する。Singleton にすると全リクエストでセッションが混線する。
            services.AddScoped<ITransactionManager, TransactionManager>();

            // セッション型ごとに ITransactionService<TSession> を登録する。
            // これを忘れると TransactionManager は InvalidOperationException を投げる。
            services.AddScoped<ITransactionService<AppSession>, AppTransactionService>();

            // --- ドメイン層 --------------------------------------------------------------------
            //
            // ここで型引数を AppSession に閉じる。ドメイン層の型定義自体には AppSession は現れない。
            // 登録するのはすべて「インターフェース → 実装」の形。上の層は具象クラスを知らない。
            services.AddScoped<IUserRepository<AppSession>, InMemoryUserRepository>();
            services.AddScoped<IUserNameDirectory<AppSession>, InMemoryUserNameDirectory>();
            services.AddScoped<IUserAggregateService<AppSession>, UserAggregateService<AppSession>>();
            services.AddScoped<IUserNameUniquenessService<AppSession>, UserNameUniquenessService<AppSession>>();

            // --- ユースケース層 ----------------------------------------------------------------
            //
            // ユースケースの TUserSession と、上で登録した集約サービスの TSession が一致することを
            // 保証しているのはこの 2 行だけ。集約ごとにストアが違うなら
            // CreateUserCommandService<AppSession, OrderStoreSession> のように別々の型を渡す。
            //
            // 口（ICreateUserCommandService）にはセッション型引数が無いので、呼び出し側は
            // AppSession を知らずに解決・実行できる（→ CreateUserAsync）。
            services.AddScoped<ICreateUserCommandService, CreateUserCommandService<AppSession>>();
            services.AddScoped<IRenameUserCommandService, RenameUserCommandService<AppSession>>();

            // クエリサービスはセッション型を持たないが、レスポンス型をクエリと 1 対 1 に固定するため
            // やはり専用の口を立てる。IQueryService<...Res> のまま登録すると、
            // 同じ形のレスポンスを返す別のクエリと衝突する。
            services.AddScoped<IListUsersQueryService, ListUsersQueryService>();
            services.AddScoped<ISearchUsersQueryService, SearchUsersQueryService>();

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// ユーザーを 1 人登録する。
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>1 リクエスト = 1 スコープ。</b> TransactionManager が Scoped なので、
        /// ユースケースごとにスコープを作る。ASP.NET Core ではフレームワークが
        /// リクエストごとに作ってくれるので、この <c>CreateScope</c> は不要になる。
        /// </para>
        /// <para>
        /// <b>ここに <see cref="AppSession"/> が出てこないことが要点。</b>
        /// 実装 <see cref="CreateUserCommandService{TUserSession}"/> はセッション型を型引数に持つが、
        /// 呼び出しに使うのはセッション型引数を持たない <see cref="ICreateUserCommandService"/> なので、
        /// コントローラー相当のコードは <c>&lt;AppSession&gt;</c> を書かずに済む。
        /// </para>
        /// </remarks>
        private static async Task<Guid> CreateUserAsync(
            IServiceProvider provider,
            string userName,
            OperateInfo operateInfo)
        {
            using var scope = provider.CreateScope();
            var commandService = scope.ServiceProvider.GetRequiredService<ICreateUserCommandService>();

            var response = await commandService.ExecuteAsync(
                new ICreateUserCommandService.Req(userName, operateInfo));
            return response.UserId;
        }

        /// <summary>
        /// 現在のユーザー一覧を出力する。
        /// </summary>
        private static async Task PrintUsersAsync(IServiceProvider provider)
        {
            using var scope = provider.CreateScope();
            var queryService = scope.ServiceProvider.GetRequiredService<IListUsersQueryService>();

            var response = await queryService.ExecuteAsync();
            Console.WriteLine($"登録済み {response.Users.Count} 件:");
            foreach (var user in response.Users)
            {
                Console.WriteLine($"  - {user.Name} ({user.UserId})");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// ユーザーを改名する。
        /// </summary>
        private static async Task RenameUserAsync(
            IServiceProvider provider,
            Guid userId,
            string newName,
            OperateInfo operateInfo)
        {
            using var scope = provider.CreateScope();
            var commandService = scope.ServiceProvider.GetRequiredService<IRenameUserCommandService>();

            await commandService.ExecuteAsync(
                new IRenameUserCommandService.Req(userId, newName, operateInfo));
        }

        /// <summary>
        /// セッションが誰に Dispose されるのかを実際に見せる。
        /// </summary>
        /// <remarks>
        /// <see cref="ITransactionService{TSession}"/> の実装側では Dispose しないのが規約で、
        /// commit / rollback / 例外のいずれの経路でも <see cref="ITransactionManager"/> が Dispose する。
        /// </remarks>
        private static async Task ShowSessionOwnershipAsync(IServiceProvider provider)
        {
            using var scope = provider.CreateScope();
            var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();

            AppSession? captured = null;
            await transactionManager.ExecuteTransactionAsync<AppSession>(
                (sessions, token) =>
                {
                    captured = sessions.GetSession<AppSession>();
                    Console.WriteLine($"  トランザクション中: IsDisposed = {captured.IsDisposed}");
                    return ValueTask.CompletedTask;
                });

            Console.WriteLine($"  コミット後:           IsDisposed = {captured?.IsDisposed}");
        }
    }
}
