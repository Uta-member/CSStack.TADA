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
            catch (ObjectAlreadyExistException exception)
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
            catch (ValueObjectLengthException exception)
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
            catch (ObjectNotFoundException exception)
            {
                Console.WriteLine($"想定どおり失敗: {exception.ObjectType?.Name} / {exception.Identifier}");
            }

            // 6. クエリサービスで絞り込む
            Console.WriteLine("== 6. 名前が 'a' で始まるユーザーを探す ==");
            using (var scope = provider.CreateScope())
            {
                var searchService = scope.ServiceProvider
                    .GetRequiredService<IQueryService<SearchUsersReq, SearchUsersRes>>();
                var found = await searchService.ExecuteAsync(new SearchUsersReq("a"));
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
            services.AddScoped<IUserRepository, InMemoryUserRepository>();
            services.AddScoped<IUserNameDirectory, InMemoryUserNameDirectory>();
            services.AddScoped<UserAggregateService>();
            services.AddScoped<UserNameUniquenessService>();

            // --- ユースケース層 ----------------------------------------------------------------
            services.AddScoped<ICommandService<CreateUserReq, CreateUserRes>, CreateUserCommandService>();
            services.AddScoped<ICommandService<RenameUserReq>, RenameUserCommandService>();
            services.AddScoped<IQueryService<ListUsersRes>, ListUsersQueryService>();
            services.AddScoped<IQueryService<SearchUsersReq, SearchUsersRes>, SearchUsersQueryService>();

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// ユーザーを 1 人登録する。
        /// </summary>
        /// <remarks>
        /// <b>1 リクエスト = 1 スコープ。</b> TransactionManager が Scoped なので、
        /// ユースケースごとにスコープを作る。ASP.NET Core ではフレームワークが
        /// リクエストごとに作ってくれるので、この <c>CreateScope</c> は不要になる。
        /// </remarks>
        private static async Task<Guid> CreateUserAsync(
            IServiceProvider provider,
            string userName,
            OperateInfo operateInfo)
        {
            using var scope = provider.CreateScope();
            var commandService = scope.ServiceProvider
                .GetRequiredService<ICommandService<CreateUserReq, CreateUserRes>>();

            var response = await commandService.ExecuteAsync(new CreateUserReq(userName, operateInfo));
            return response.UserId;
        }

        /// <summary>
        /// 現在のユーザー一覧を出力する。
        /// </summary>
        private static async Task PrintUsersAsync(IServiceProvider provider)
        {
            using var scope = provider.CreateScope();
            var queryService = scope.ServiceProvider.GetRequiredService<IQueryService<ListUsersRes>>();

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
            var commandService = scope.ServiceProvider.GetRequiredService<ICommandService<RenameUserReq>>();

            await commandService.ExecuteAsync(new RenameUserReq(userId, newName, operateInfo));
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
