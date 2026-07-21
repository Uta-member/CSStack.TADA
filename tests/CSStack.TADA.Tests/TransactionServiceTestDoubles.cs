namespace CSStack.TADA.Tests
{
	/// <summary>
	/// Records every begin / commit / rollback / dispose in the order it happened.
	/// </summary>
	internal sealed class TransactionLog
	{
		public List<string> Entries { get; } = new();

		public void Add(string entry)
		{
			Entries.Add(entry);
		}
	}

	internal abstract class TestSession : IDisposable
	{
		private readonly TransactionLog _log;
		private readonly string _name;

		protected TestSession(TransactionLog log, string name)
		{
			_log = log;
			_name = name;
		}

		public int DisposeCount { get; private set; }

		public Exception? DisposeException { get; set; }

		public void Dispose()
		{
			DisposeCount++;
			_log.Add($"dispose:{_name}");
			if (DisposeException is not null)
			{
				throw DisposeException;
			}
		}
	}

	internal sealed class SessionA : TestSession
	{
		public SessionA(TransactionLog log)
			: base(log, "A")
		{
		}
	}

	internal sealed class SessionB : TestSession
	{
		public SessionB(TransactionLog log)
			: base(log, "B")
		{
		}
	}

	internal sealed class SessionC : TestSession
	{
		public SessionC(TransactionLog log)
			: base(log, "C")
		{
		}
	}

	/// <summary>
	/// A session type that is deliberately not registered with the service provider.
	/// </summary>
	internal sealed class UnregisteredSession : IDisposable
	{
		public void Dispose()
		{
		}
	}

	internal sealed class TestTransactionService<TSession> : ITransactionService<TSession>
		where TSession : TestSession
	{
		private readonly Func<TSession> _factory;
		private readonly TransactionLog _log;
		private readonly string _name;

		public TestTransactionService(TransactionLog log, string name, Func<TSession> factory)
		{
			_log = log;
			_name = name;
			_factory = factory;
		}

		public Exception? BeginException { get; set; }

		public Exception? CommitException { get; set; }

		public CancellationToken LastBeginToken { get; private set; }

		public CancellationToken LastCommitToken { get; private set; }

		public CancellationToken LastRollbackToken { get; private set; }

		public TSession? LastSession { get; private set; }

		public Exception? RollbackException { get; set; }

		public ValueTask<TSession> BeginAsync(CancellationToken cancellationToken = default)
		{
			_log.Add($"begin:{_name}");
			LastBeginToken = cancellationToken;
			if (BeginException is not null)
			{
				throw BeginException;
			}
			LastSession = _factory();
			return ValueTask.FromResult(LastSession);
		}

		public ValueTask CommitAsync(TSession session, CancellationToken cancellationToken = default)
		{
			_log.Add($"commit:{_name}");
			LastCommitToken = cancellationToken;
			if (CommitException is not null)
			{
				throw CommitException;
			}
			return ValueTask.CompletedTask;
		}

		public ValueTask RollbackAsync(TSession session, CancellationToken cancellationToken = default)
		{
			_log.Add($"rollback:{_name}");
			LastRollbackToken = cancellationToken;
			if (RollbackException is not null)
			{
				throw RollbackException;
			}
			return ValueTask.CompletedTask;
		}
	}

	internal sealed class TestServiceProvider : IServiceProvider
	{
		private readonly Dictionary<Type, object> _services = new();

		public object? GetService(Type serviceType)
		{
			return _services.TryGetValue(serviceType, out var service) ? service : null;
		}

		public void Register<TSession>(ITransactionService<TSession> service) where TSession : IDisposable
		{
			_services[typeof(ITransactionService<TSession>)] = service;
		}
	}
}
