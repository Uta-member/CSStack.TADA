using System.Collections.Immutable;

namespace CSStack.TADA.Tests
{
    public class TransactionManagerTests
    {
        private readonly TransactionLog _log = new();
        private readonly TransactionManager _manager;
        private readonly TestServiceProvider _provider = new();
        private readonly TestTransactionService<SessionA> _serviceA;
        private readonly TestTransactionService<SessionB> _serviceB;
        private readonly TestTransactionService<SessionC> _serviceC;

        public TransactionManagerTests()
        {
            _serviceA = new TestTransactionService<SessionA>(_log, "A", () => new SessionA(_log));
            _serviceB = new TestTransactionService<SessionB>(_log, "B", () => new SessionB(_log));
            _serviceC = new TestTransactionService<SessionC>(_log, "C", () => new SessionC(_log));
            _provider.Register(_serviceA);
            _provider.Register(_serviceB);
            _provider.Register(_serviceC);
            _manager = new TransactionManager(_provider);
        }

        [Fact]
        public async Task BeginTransactionAsync_IsIdempotentForTheSameSessionType()
        {
            await _manager.BeginTransactionAsync<SessionA>();
            await _manager.BeginTransactionAsync<SessionA>();

            Assert.Equal(new[] { "begin:A" }, _log.Entries);
        }

        [Fact]
        public async Task BeginTransactionAsync_NonDisposableSessionType_ThrowsArgumentException()
        {
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await _manager.BeginTransactionAsync(typeof(string)));
        }

        [Fact]
        public async Task BeginTransactionAsync_UnregisteredSessionType_ThrowsInvalidOperationException()
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _manager.BeginTransactionAsync<UnregisteredSession>());

            Assert.Contains(nameof(UnregisteredSession), exception.Message);
        }

        [Fact]
        public async Task BeginTransactionsAsync_BeginFailure_RollsBackAndDisposesTheSessionsAlreadyBegun()
        {
            var beginFailure = new InvalidOperationException("begin B failed");
            _serviceB.BeginException = beginFailure;

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _manager.BeginTransactionsAsync(
                    ImmutableList.Create(typeof(SessionA), typeof(SessionB))));

            Assert.Same(beginFailure, thrown);
            Assert.Equal(new[] { "begin:A", "begin:B", "rollback:A", "dispose:A" }, _log.Entries);
            Assert.False(_manager.TryGetSession<SessionA>(out _));
        }

        [Fact]
        public async Task BeginTransactionsAsync_BeginAndRollbackBothFail_ThrowsAggregateExceptionWithBoth()
        {
            var beginFailure = new InvalidOperationException("begin B failed");
            var rollbackFailure = new InvalidOperationException("rollback A failed");
            _serviceB.BeginException = beginFailure;
            _serviceA.RollbackException = rollbackFailure;

            var thrown = await Assert.ThrowsAsync<AggregateException>(
                async () => await _manager.BeginTransactionsAsync(
                    ImmutableList.Create(typeof(SessionA), typeof(SessionB))));

            Assert.Equal(new Exception[] { beginFailure, rollbackFailure }, thrown.InnerExceptions);
        }

        [Fact]
        public async Task CommitTransactionsAsync_CommitsInOrderThenDisposesInReverseOrder()
        {
            await _manager.BeginTransactionAsync<SessionA>();
            await _manager.BeginTransactionAsync<SessionB>();

            await _manager.CommitTransactionsAsync();

            Assert.Equal(
                new[] { "begin:A", "begin:B", "commit:A", "commit:B", "dispose:B", "dispose:A" },
                _log.Entries);
            Assert.Equal(1, _serviceA.LastSession!.DisposeCount);
            Assert.Equal(1, _serviceB.LastSession!.DisposeCount);
        }

        [Fact]
        public async Task CommitTransactionsAsync_CommitFailure_RollsBackTheSessionsThatAreNotCommittedYet()
        {
            var commitFailure = new InvalidOperationException("commit failed");
            _serviceB.CommitException = commitFailure;

            await _manager.BeginTransactionAsync<SessionA>();
            await _manager.BeginTransactionAsync<SessionB>();
            await _manager.BeginTransactionAsync<SessionC>();

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _manager.CommitTransactionsAsync());

            Assert.Same(commitFailure, thrown);

            // A is already committed and cannot be undone. C and the failed B are rolled back in reverse order.
            Assert.Equal(
                new[]
                {
                    "begin:A", "begin:B", "begin:C", "commit:A", "commit:B", "rollback:C", "rollback:B",
                    "dispose:C", "dispose:B", "dispose:A",
                },
                _log.Entries);
        }

        [Fact]
        public async Task CommitTransactionsAsync_CommitAndRollbackBothFail_ThrowsAggregateExceptionWithBoth()
        {
            var commitFailure = new InvalidOperationException("commit failed");
            var rollbackFailure = new InvalidOperationException("rollback failed");
            _serviceA.CommitException = commitFailure;
            _serviceA.RollbackException = rollbackFailure;

            await _manager.BeginTransactionAsync<SessionA>();

            var thrown = await Assert.ThrowsAsync<AggregateException>(
                async () => await _manager.CommitTransactionsAsync());

            Assert.Equal(new Exception[] { commitFailure, rollbackFailure }, thrown.InnerExceptions);
        }

        [Fact]
        public async Task CommitTransactionsAsync_DisposeFailure_IsReported()
        {
            var disposeFailure = new InvalidOperationException("dispose failed");
            await _manager.BeginTransactionAsync<SessionA>();
            _serviceA.LastSession!.DisposeException = disposeFailure;

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _manager.CommitTransactionsAsync());

            Assert.Same(disposeFailure, thrown);
        }

        [Fact]
        public async Task RollbackTransactionsAsync_RollsBackEverySessionEvenWhenOneFails()
        {
            _serviceB.RollbackException = new InvalidOperationException("rollback B failed");

            await _manager.BeginTransactionAsync<SessionA>();
            await _manager.BeginTransactionAsync<SessionB>();
            await _manager.BeginTransactionAsync<SessionC>();

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _manager.RollbackTransactionsAsync());

            Assert.Equal("rollback B failed", thrown.Message);

            // The failure on B must not skip A, and every session must still be disposed.
            Assert.Equal(
                new[]
                {
                    "begin:A", "begin:B", "begin:C", "rollback:C", "rollback:B", "rollback:A",
                    "dispose:C", "dispose:B", "dispose:A",
                },
                _log.Entries);
        }

        [Fact]
        public async Task RollbackTransactionsAsync_SeveralFailures_ThrowsAggregateException()
        {
            var failureA = new InvalidOperationException("rollback A failed");
            var failureB = new InvalidOperationException("rollback B failed");
            _serviceA.RollbackException = failureA;
            _serviceB.RollbackException = failureB;

            await _manager.BeginTransactionAsync<SessionA>();
            await _manager.BeginTransactionAsync<SessionB>();

            var thrown = await Assert.ThrowsAsync<AggregateException>(
                async () => await _manager.RollbackTransactionsAsync());

            Assert.Equal(new Exception[] { failureB, failureA }, thrown.InnerExceptions);
        }

        [Fact]
        public async Task ExecuteTransactionAsync_CommitsAndDisposesEverySession()
        {
            await _manager.ExecuteTransactionAsync(
                ImmutableList.Create(typeof(SessionA), typeof(SessionB)),
                (sessions, _) =>
                {
                    _log.Add("body");
                    Assert.Same(_serviceA.LastSession, sessions.GetSession<SessionA>());
                    Assert.Same(_serviceB.LastSession, sessions.GetSession<SessionB>());
                    return ValueTask.CompletedTask;
                });

            Assert.Equal(
                new[] { "begin:A", "begin:B", "body", "commit:A", "commit:B", "dispose:B", "dispose:A" },
                _log.Entries);
        }

        [Fact]
        public async Task ExecuteTransactionAsync_GenericOverloadBeginsTheGivenSessionTypes()
        {
            await _manager.ExecuteTransactionAsync<SessionA, SessionB>((_, _) => ValueTask.CompletedTask);

            Assert.Equal(
                new[] { "begin:A", "begin:B", "commit:A", "commit:B", "dispose:B", "dispose:A" },
                _log.Entries);
        }

        [Fact]
        public async Task ExecuteTransactionAsync_BodyFailure_RollsBackDisposesAndRethrows()
        {
            var bodyFailure = new InvalidOperationException("body failed");

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _manager.ExecuteTransactionAsync<SessionA, SessionB>(
                    (_, _) => throw bodyFailure));

            Assert.Same(bodyFailure, thrown);
            Assert.Equal(
                new[] { "begin:A", "begin:B", "rollback:B", "rollback:A", "dispose:B", "dispose:A" },
                _log.Entries);
            Assert.Equal(1, _serviceA.LastSession!.DisposeCount);
            Assert.Equal(1, _serviceB.LastSession!.DisposeCount);
        }

        [Fact]
        public async Task ExecuteTransactionAsync_BeginFailure_RollsBackAndDisposesTheSessionsAlreadyBegun()
        {
            _serviceB.BeginException = new InvalidOperationException("begin B failed");

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _manager.ExecuteTransactionAsync<SessionA, SessionB>(
                    (_, _) => ValueTask.CompletedTask));

            Assert.Equal(new[] { "begin:A", "begin:B", "rollback:A", "dispose:A" }, _log.Entries);
        }

        [Fact]
        public async Task ExecuteTransactionAsync_BeforeRollbackHandlerRunsBeforeTheRollback()
        {
            var bodyFailure = new InvalidOperationException("body failed");
            Exception? handled = null;

            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _manager.ExecuteTransactionAsync<SessionA>(
                    (_, _) => throw bodyFailure,
                    exception =>
                    {
                        handled = exception;
                        _log.Add("handler");
                        return ValueTask.CompletedTask;
                    }));

            Assert.Same(bodyFailure, handled);
            Assert.Equal(new[] { "begin:A", "handler", "rollback:A", "dispose:A" }, _log.Entries);
        }

        [Fact]
        public async Task ExecuteTransactionAsync_BeforeRollbackHandlerFailure_StillRollsBackAndReportsBoth()
        {
            var bodyFailure = new InvalidOperationException("body failed");
            var handlerFailure = new InvalidOperationException("handler failed");

            var thrown = await Assert.ThrowsAsync<AggregateException>(
                async () => await _manager.ExecuteTransactionAsync<SessionA>(
                    (_, _) => throw bodyFailure,
                    _ => throw handlerFailure));

            Assert.Equal(new Exception[] { bodyFailure, handlerFailure }, thrown.InnerExceptions);
            Assert.Equal(new[] { "begin:A", "rollback:A", "dispose:A" }, _log.Entries);
        }

        [Fact]
        public async Task ExecuteTransactionAsync_CommitFailure_InvokesTheHandlerAfterTheRollbackAndDispose()
        {
            var commitFailure = new InvalidOperationException("commit failed");
            _serviceA.CommitException = commitFailure;
            var disposeCountWhenHandlerRan = -1;

            var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _manager.ExecuteTransactionAsync<SessionA>(
                    (_, _) => ValueTask.CompletedTask,
                    _ =>
                    {
                        _log.Add("handler");
                        disposeCountWhenHandlerRan = _serviceA.LastSession!.DisposeCount;
                        return ValueTask.CompletedTask;
                    }));

            Assert.Same(commitFailure, thrown);

            // CommitTransactionsAsync rolls back and disposes before it throws, so on this path the handler
            // runs last and the session it sees is already disposed.
            Assert.Equal(new[] { "begin:A", "commit:A", "rollback:A", "dispose:A", "handler" }, _log.Entries);
            Assert.Equal(1, disposeCountWhenHandlerRan);
        }

        [Fact]
        public async Task ExecuteTransactionAsync_NestedCallWithAnotherSessionType_ThrowsNestedTransactionException()
        {
            NestedTransactionException? nested = null;

            await _manager.ExecuteTransactionAsync<SessionA>(
                async (sessions, token) =>
                {
                    nested = await Assert.ThrowsAsync<NestedTransactionException>(
                        async () => await _manager.ExecuteTransactionAsync<SessionB>(
                            (_, _) =>
                            {
                                _log.Add("inner-body");
                                return ValueTask.CompletedTask;
                            },
                            cancellationToken: token));

                    // The outer session is untouched: not committed, not disposed, still reachable.
                    _log.Add("outer-body");
                    Assert.Same(_serviceA.LastSession, sessions.GetSession<SessionA>());
                    Assert.Equal(0, _serviceA.LastSession!.DisposeCount);
                });

            Assert.IsAssignableFrom<TADAException>(nested);
            Assert.Equal(new[] { "begin:A", "outer-body", "commit:A", "dispose:A" }, _log.Entries);
        }

        [Fact]
        public async Task ExecuteTransactionAsync_NestedCallWithTheSameSessionType_ThrowsNestedTransactionException()
        {
            await _manager.ExecuteTransactionAsync<SessionA>(
                async (sessions, token) =>
                {
                    await Assert.ThrowsAsync<NestedTransactionException>(
                        async () => await _manager.ExecuteTransactionAsync<SessionA>(
                            (_, _) =>
                            {
                                _log.Add("inner-body");
                                return ValueTask.CompletedTask;
                            },
                            cancellationToken: token));

                    _log.Add("outer-body");
                    Assert.Same(_serviceA.LastSession, sessions.GetSession<SessionA>());
                    Assert.Equal(0, _serviceA.LastSession!.DisposeCount);
                });

            Assert.Equal(new[] { "begin:A", "outer-body", "commit:A", "dispose:A" }, _log.Entries);
        }

        [Fact]
        public async Task ExecuteTransactionAsync_NestedCallFromTheBeforeRollbackHandler_ThrowsNestedTransactionException()
        {
            var bodyFailure = new InvalidOperationException("body failed");

            var thrown = await Assert.ThrowsAsync<AggregateException>(
                async () => await _manager.ExecuteTransactionAsync<SessionA>(
                    (_, _) => throw bodyFailure,
                    async _ => await _manager.ExecuteTransactionAsync<SessionB>(
                        (_, _) => ValueTask.CompletedTask)));

            Assert.Same(bodyFailure, thrown.InnerExceptions[0]);
            Assert.IsType<NestedTransactionException>(thrown.InnerExceptions[1]);
            Assert.Equal(new[] { "begin:A", "rollback:A", "dispose:A" }, _log.Entries);
        }

        [Fact]
        public async Task ExecuteTransactionAsync_ConsecutiveCallsOnTheSameInstanceAreAllowed()
        {
            await _manager.ExecuteTransactionAsync<SessionA>((_, _) => ValueTask.CompletedTask);
            await _manager.ExecuteTransactionAsync<SessionA>((_, _) => ValueTask.CompletedTask);

            Assert.Equal(
                new[] { "begin:A", "commit:A", "dispose:A", "begin:A", "commit:A", "dispose:A" },
                _log.Entries);
        }

        [Fact]
        public async Task ExecuteTransactionAsync_FailedTransaction_DoesNotBlockTheNextOne()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _manager.ExecuteTransactionAsync<SessionA>(
                    (_, _) => throw new InvalidOperationException("body failed")));

            await _manager.ExecuteTransactionAsync<SessionA>((_, _) => ValueTask.CompletedTask);

            Assert.Equal(
                new[] { "begin:A", "rollback:A", "dispose:A", "begin:A", "commit:A", "dispose:A" },
                _log.Entries);
        }

        [Fact]
        public async Task ExecuteTransactionAsync_TransactionSessionsIsACopyOfTheSessionsThatWereRequested()
        {
            TransactionSessions? captured = null;

            await _manager.ExecuteTransactionAsync<SessionA>(
                async (sessions, token) =>
                {
                    captured = sessions;
                    await _manager.BeginTransactionAsync<SessionB>(token);

                    // A session begun after the call started is not reachable through the given set.
                    Assert.False(sessions.TryGetSession<SessionB>(out _));
                });

            // The set is a copy, so committing the sessions does not empty it.
            Assert.Same(_serviceA.LastSession, captured!.GetSession<SessionA>());
        }

        [Fact]
        public async Task ExecuteTransactionAsync_PassesTheCancellationTokenToTheServicesAndTheBody()
        {
            using var cts = new CancellationTokenSource();
            CancellationToken bodyToken = default;

            await _manager.ExecuteTransactionAsync<SessionA>(
                (_, token) =>
                {
                    bodyToken = token;
                    return ValueTask.CompletedTask;
                },
                cancellationToken: cts.Token);

            Assert.Equal(cts.Token, bodyToken);
            Assert.Equal(cts.Token, _serviceA.LastBeginToken);
            Assert.Equal(cts.Token, _serviceA.LastCommitToken);
        }

        [Fact]
        public async Task ExecuteTransactionAsync_Cancellation_StillRollsBackWithAnUncancelledToken()
        {
            using var cts = new CancellationTokenSource();

            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await _manager.ExecuteTransactionAsync<SessionA>(
                    (_, token) =>
                    {
                        cts.Cancel();
                        token.ThrowIfCancellationRequested();
                        return ValueTask.CompletedTask;
                    },
                    cancellationToken: cts.Token));

            Assert.Equal(new[] { "begin:A", "rollback:A", "dispose:A" }, _log.Entries);
            Assert.False(_serviceA.LastRollbackToken.CanBeCanceled);
        }

        [Fact]
        public async Task GetSession_SessionNotBegun_ThrowsTransactionSessionNotFoundException()
        {
            await _manager.BeginTransactionAsync<SessionA>();

            var exception = Assert.Throws<TransactionSessionNotFoundException>(() => _manager.GetSession<SessionB>());

            Assert.Equal(typeof(SessionB), exception.SessionType);
            Assert.Contains(nameof(SessionB), exception.Message);
            Assert.IsAssignableFrom<TADAException>(exception);
        }

        [Fact]
        public async Task TryGetSession_ReportsWhetherTheSessionHasBegun()
        {
            await _manager.BeginTransactionAsync<SessionA>();

            Assert.True(_manager.TryGetSession<SessionA>(out var sessionA));
            Assert.Same(_serviceA.LastSession, sessionA);
            Assert.False(_manager.TryGetSession<SessionB>(out var sessionB));
            Assert.Null(sessionB);
        }

        [Fact]
        public async Task TransactionSessions_GetSession_SessionNotBegun_ThrowsTransactionSessionNotFoundException()
        {
            TransactionSessions? captured = null;

            await _manager.ExecuteTransactionAsync<SessionA>(
                (sessions, _) =>
                {
                    captured = sessions;
                    return ValueTask.CompletedTask;
                });

            var exception = Assert.Throws<TransactionSessionNotFoundException>(
                () => captured!.GetSession<SessionB>());

            Assert.Equal(typeof(SessionB), exception.SessionType);
            Assert.Contains("ExecuteTransactionAsync", exception.Message);
        }

        [Fact]
        public void GetTransactionService_ResolvesThroughTheNonGenericBaseInterface()
        {
            Assert.Same(_serviceA, _manager.GetTransactionService<SessionA>());
            Assert.Same(_serviceA, _manager.GetTransactionService(typeof(SessionA)));
        }
    }
}
