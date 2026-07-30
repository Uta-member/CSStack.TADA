namespace CSStack.TADA.Tests
{
    /// <summary>
    /// <see cref="AggregateServiceBase{TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession}"/> の
    /// コンストラクタの null チェックと、<c>GetEntityByIdentifierAsync</c> がリポジトリへ委譲することを固定するテスト。
    /// </summary>
    public class AggregateServiceBaseTests
    {
        private sealed class SampleAggregateService
            : AggregateServiceBase<User, UserId, InMemoryUserRepository, OperateInfo, FakeSession>
        {
            public SampleAggregateService(InMemoryUserRepository repository)
                : base(repository)
            {
            }
        }

        [Fact]
        public void コンストラクタにnullを渡すとArgumentNullExceptionを投げる()
        {
            Assert.Throws<ArgumentNullException>(() => new SampleAggregateService(null!));
        }

        [Fact]
        public async Task GetEntityByIdentifierAsyncはリポジトリのFindByIdentifierAsyncへ委譲する()
        {
            var store = new InMemoryUserStore();
            var repository = new InMemoryUserRepository();
            var service = new SampleAggregateService(repository);
            var id = UserId.New();
            store.Set(new UserRow(id.Value, "taro", new OperateInfo("operator-1", DateTimeOffset.UnixEpoch)));

            using var session = new FakeSession(store);
            var found = await service.GetEntityByIdentifierAsync(session, id);

            Assert.True(found.HasValue);
            Assert.Equal(id, found.Value!.Identifier);
        }
    }
}
