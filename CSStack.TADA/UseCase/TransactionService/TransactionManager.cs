using System.Collections.Immutable;

namespace CSStack.TADA
{
    /// <summary>
    /// Transaction manager class.
    /// </summary>
    public class TransactionManager : ITransactionManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<Type, dynamic> _sessions = new();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="serviceProvider"></param>
        public TransactionManager(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private ITransactionService<TSession> GetTransactionService<TSession>()
            where TSession : IDisposable
        {
            var service = _serviceProvider.GetService(typeof(ITransactionService<>).MakeGenericType(typeof(TSession)));
            if(service == null)
            {
                throw new InvalidOperationException($"No service found for {typeof(TSession).Name}");
            }
            return (ITransactionService<TSession>)service;
        }

        private dynamic GetTransactionServiceBySessionType(Type sessionType)
        {
            var serviceType = typeof(ITransactionService<>).MakeGenericType(sessionType);
            var service = _serviceProvider.GetService(serviceType);
            if(service == null)
            {
                throw new InvalidOperationException($"No service found for {sessionType.Name}");
            }
            return service;
        }

        /// <summary>
        /// Begin a transaction.
        /// </summary>
        /// <typeparam name="TSession"></typeparam>
        /// <returns></returns>
        public async ValueTask BeginTransactionAsync<TSession>()
            where TSession : IDisposable
        {
            if(_sessions.ContainsKey(typeof(TSession)))
            {
                return;
            }
            var transactionService = GetTransactionService<TSession>();
            var session = await transactionService.BeginAsync();
            _sessions.Add(typeof(TSession), session);
        }

        /// <summary>
        /// Begin a transaction.
        /// </summary>
        /// <returns></returns>
        public async ValueTask BeginTransactionAsync(Type sessionType)
        {
            if(_sessions.ContainsKey(sessionType))
            {
                return;
            }
            var transactionService = GetTransactionServiceBySessionType(sessionType);
            var session = await transactionService.BeginAsync();
            _sessions.Add(sessionType, session);
        }

        /// <summary>
        /// Begin multiple transactions.
        /// </summary>
        /// <param name="sessionTypes"></param>
        /// <returns></returns>
        public async ValueTask BeginTransactionsAsync(ImmutableList<Type> sessionTypes)
        {
            foreach(var sessionType in sessionTypes)
            {
                await BeginTransactionAsync(sessionType);
            }
        }

        /// <summary>
        /// Commit transactions.
        /// </summary>
        public async ValueTask CommitAsync()
        {
            foreach(var session in _sessions)
            {
                var transactionService = GetTransactionServiceBySessionType(session.Key);
                await transactionService.CommitAsync(session.Value);
            }
            _sessions.Clear();
        }

        /// <summary>
        /// Execute a transaction.
        /// </summary>
        /// <param name="sessionTypes"></param>
        /// <param name="transactionFunction"></param>
        /// <param name="beforeRollbackHandler"></param>
        /// <returns></returns>
        public async ValueTask ExecuteTransactionAsync(
            ImmutableList<Type> sessionTypes,
            Func<TransactionSessions, ValueTask> transactionFunction,
            Func<Exception, ValueTask>? beforeRollbackHandler = null)
        {
            try
            {
                await BeginTransactionsAsync(sessionTypes);
                await transactionFunction.Invoke(new TransactionSessions(Sessions));
                await CommitAsync();
            }
            catch(Exception ex)
            {
                if(beforeRollbackHandler != null)
                {
                    await beforeRollbackHandler.Invoke(ex);
                }
                await RollbackAsync();
                throw;
            }
            finally
            {
                _sessions.Clear();
            }
        }

        /// <summary>
        /// Get transaction session factor.
        /// </summary>
        /// <typeparam name="TSession"></typeparam>
        /// <returns></returns>
        public TSession GetSession<TSession>()
            where TSession : IDisposable
        {
            return (TSession)_sessions[typeof(TSession)];
        }

        /// <summary>
        /// Get transaction session factor.
        /// </summary>
        /// <param name="sessionType"></param>
        /// <returns></returns>
        public object GetSession(Type sessionType)
        {
            return _sessions[sessionType];
        }

        /// <summary>
        /// Rollback transactions.
        /// </summary>
        public async ValueTask RollbackAsync()
        {
            foreach(var session in _sessions)
            {
                var transactionService = GetTransactionServiceBySessionType(session.Key);
                await transactionService.RollbackAsync(session.Value);
            }
            _sessions.Clear();
        }

        /// <summary>
        /// Transaction sessions.
        /// </summary>
        public IReadOnlyDictionary<Type, dynamic> Sessions => _sessions;
    }
}
