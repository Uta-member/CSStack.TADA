namespace CSStack.TADA.Tests
{
    /// <summary>
    /// Walks the sample domain end to end: a command service opens a transaction through the
    /// <see cref="TransactionManager"/>, the repository reads and writes inside it, and the session is
    /// committed (or rolled back) and disposed. This doubles as the "correct usage of TADA" example, and
    /// pins the behaviours the CLAUDE.md landmines warn about (not-found is Empty, the boundary is the
    /// command service, the manager owns session disposal).
    /// </summary>
    public class SampleUserDomainTests
    {
        private readonly CreateUserCommandService _commandService;
        private readonly OperateInfo _operateInfo = new("operator-1", DateTimeOffset.UnixEpoch);
        private readonly TestServiceProvider _provider = new();
        private readonly InMemoryUserRepository _repository = new();
        private readonly InMemoryUserStore _store = new();
        private readonly TransactionManager _transactionManager;
        private readonly FakeTransactionService _transactionService;

        public SampleUserDomainTests()
        {
            _transactionService = new FakeTransactionService(_store);
            _provider.Register(_transactionService);
            _transactionManager = new TransactionManager(_provider);
            _commandService = new CreateUserCommandService(_transactionManager, _repository);
        }

        [Fact]
        public async Task コマンド成功時にユーザーが永続化されセッションがコミットされ破棄される()
        {
            var id = Guid.NewGuid();

            await _commandService.ExecuteAsync(new CreateUserReq(id, "taro", _operateInfo));

            Assert.True(_store.TryGet(id, out var row));
            Assert.Equal("taro", row.Name);
            Assert.Same(_operateInfo, row.OperateInfo);
            Assert.True(_transactionService.LastSession!.IsCommitted);
            Assert.True(_transactionService.LastSession!.IsDisposed);
            Assert.False(_transactionService.LastSession!.IsRolledBack);
        }

        [Fact]
        public async Task 重複登録はUserAlreadyExistsExceptionでロールバックされ既存行は変わらない()
        {
            var id = Guid.NewGuid();
            _store.Set(new UserRow(id, "既存の名前", _operateInfo));

            var exception = await Assert.ThrowsAsync<UserAlreadyExistsException>(
                async () => await _commandService.ExecuteAsync(new CreateUserReq(id, "taro", _operateInfo)));

            Assert.Equal(typeof(User), exception.ObjectType);
            Assert.True(_store.TryGet(id, out var row));
            Assert.Equal("既存の名前", row.Name);
            Assert.True(_transactionService.LastSession!.IsRolledBack);
            Assert.True(_transactionService.LastSession!.IsDisposed);
            Assert.False(_transactionService.LastSession!.IsCommitted);
        }

        [Fact]
        public async Task 不正な名前は値オブジェクトの例外でロールバックされ何も永続化されない()
        {
            var id = Guid.NewGuid();
            var tooLong = new string('a', UserName.MaxLength + 1);

            await Assert.ThrowsAsync<UserNameLengthException>(
                async () => await _commandService.ExecuteAsync(new CreateUserReq(id, tooLong, _operateInfo)));

            Assert.Equal(0, _store.Count);
            Assert.True(_transactionService.LastSession!.IsRolledBack);
            Assert.True(_transactionService.LastSession!.IsDisposed);
        }

        [Fact]
        public void Validateは復元されたエンティティの値オブジェクトの不変条件違反を伝播する()
        {
            var tooLong = new string('a', UserName.MaxLength + 1);
            var user = User.Reconstruct(UserId.New(), UserName.Reconstruct(tooLong));

            Assert.Throws<UserNameLengthException>(user.Validate);
        }

        [Fact]
        public async Task リポジトリはコミット前でも同一セッション内の書き込みを読み返せる()
        {
            var id = UserId.New();
            var seenBeforeSave = true;
            var seenAfterSave = false;

            await _transactionManager.ExecuteTransactionAsync<FakeSession>(
                async (sessions, token) =>
                {
                    var session = sessions.GetSession<FakeSession>();

                    seenBeforeSave = (await _repository.FindByIdentifierAsync(session, id, token)).HasValue;
                    await _repository.SaveAsync(session, User.Create(id, UserName.Create("taro")), _operateInfo, token);
                    seenAfterSave = (await _repository.FindByIdentifierAsync(session, id, token)).HasValue;
                });

            Assert.False(seenBeforeSave);
            Assert.True(seenAfterSave);
            Assert.True(_store.Contains(id.Value));
        }

        [Fact]
        public async Task FindByIdentifierAsyncは不在時にOptionalEmptyを返す()
        {
            var found = Optional<User>.Some(User.Create(UserId.New(), UserName.Create("dummy")));

            await _transactionManager.ExecuteTransactionAsync<FakeSession>(
                async (sessions, token) =>
                {
                    found = await _repository.FindByIdentifierAsync(sessions.GetSession<FakeSession>(), UserId.New(), token);
                });

            Assert.Equal(Optional<User>.Empty, found);
            Assert.False(found.HasValue);
        }

        [Fact]
        public async Task 読み出したエンティティはReconstruct経由で識別子が一致する()
        {
            var id = UserId.New();
            _store.Set(new UserRow(id.Value, "taro", _operateInfo));
            User? restored = null;

            await _transactionManager.ExecuteTransactionAsync<FakeSession>(
                async (sessions, token) =>
                {
                    restored = (await _repository.FindByIdentifierAsync(sessions.GetSession<FakeSession>(), id, token))
                        .Value;
                });

            Assert.NotNull(restored);
            Assert.Equal(id, restored!.Identifier);
            Assert.Equal("taro", restored.Name.Value);
        }

        [Fact]
        public async Task DeleteAsyncは行を削除しコミットで確定する()
        {
            var id = UserId.New();
            _store.Set(new UserRow(id.Value, "taro", _operateInfo));

            await _transactionManager.ExecuteTransactionAsync<FakeSession>(
                async (sessions, token) =>
                {
                    var session = sessions.GetSession<FakeSession>();
                    var user = (await _repository.FindByIdentifierAsync(session, id, token)).Value!;
                    await _repository.DeleteAsync(session, user, _operateInfo, token);
                });

            Assert.False(_store.Contains(id.Value));
        }

        [Fact]
        public async Task DeleteのステージングはロールバックされるとStoreに反映されない()
        {
            var id = UserId.New();
            _store.Set(new UserRow(id.Value, "taro", _operateInfo));
            var failure = new InvalidOperationException("boom");

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _transactionManager.ExecuteTransactionAsync<FakeSession>(
                    async (sessions, token) =>
                    {
                        var session = sessions.GetSession<FakeSession>();
                        var user = (await _repository.FindByIdentifierAsync(session, id, token)).Value!;
                        await _repository.DeleteAsync(session, user, _operateInfo, token);
                        throw failure;
                    }));

            // The delete was staged but the transaction rolled back, so the row is still there.
            Assert.True(_store.Contains(id.Value));
            Assert.True(_transactionService.LastSession!.IsRolledBack);
        }
    }
}
