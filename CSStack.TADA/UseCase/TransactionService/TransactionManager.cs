using System.Collections.Immutable;

namespace CSStack.TADA
{
    /// <summary>
    /// トランザクション管理クラス
    /// </summary>
    /// <typeparam name="TSessionIdentifier"></typeparam>
    public sealed class TransactionManager<TSessionIdentifier> : ITransactionManager<TSessionIdentifier>
        where TSessionIdentifier : Enum
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ITransactionTypeResolver<TSessionIdentifier> _transactionTypeResolver;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="transactionTypeResolver"></param>
        public TransactionManager(
            IServiceProvider serviceProvider,
            ITransactionTypeResolver<TSessionIdentifier> transactionTypeResolver)
        {
            _serviceProvider = serviceProvider;
            _transactionTypeResolver = transactionTypeResolver;
        }

        /// <summary>
        /// トランザクションを実行する
        /// </summary>
        /// <param name="transactionTargets"></param>
        /// <param name="transactionAsyncFunction"></param>
        /// <param name="rollbackHandler"></param>
        /// <returns></returns>
        public async ValueTask ExecuteTransactionAsync(
            ImmutableList<TSessionIdentifier> transactionTargets,
            Func<Dictionary<TSessionIdentifier, IDisposable>, ValueTask> transactionAsyncFunction,
            Func<Exception, ValueTask>? rollbackHandler = null)
        {
            var sessionDictionary = new Dictionary<TSessionIdentifier, IDisposable>();
            var transactionServices = new Dictionary<Type, KeyValuePair<ITransactionService<IDisposable>, IDisposable>>(
                );
            try
            {
                foreach (var transactionTarget in transactionTargets)
                {
                    var sessionType = _transactionTypeResolver.GetSessionType(transactionTarget);
                    if (!transactionServices.ContainsKey(sessionType))
                    {
                        var transactionService = GetTransactionService(sessionType);
                        using var session = await transactionService.BeginAsync();
                        transactionServices.Add(
                            sessionType,
                            new KeyValuePair<ITransactionService<IDisposable>, IDisposable>(transactionService, session));
                        sessionDictionary.Add(transactionTarget, session);
                    }
                    else
                    {
                        sessionDictionary.Add(transactionTarget, transactionServices[sessionType].Value);
                    }
                }
            }
            catch
            {
                sessionDictionary.Clear();
                transactionServices.Clear();
                throw;
            }

            try
            {
                await transactionAsyncFunction(sessionDictionary);
                foreach (var transactionService in transactionServices)
                {
                    await transactionService.Value.Key.CommitAsync(transactionService.Value.Value);
                }
            }
            catch (Exception ex)
            {
                rollbackHandler?.Invoke(ex);
                foreach (var transactionService in transactionServices)
                {
                    await transactionService.Value.Key.RollbackAsync(transactionService.Value.Value);
                }
                throw;
            }
            finally
            {
                // 一応Disposeする
                foreach (var session in sessionDictionary.Values)
                {
                    session.Dispose();
                }
                sessionDictionary.Clear();
                transactionServices.Clear();
            }
        }

        /// <summary>
        /// トランザクションサービスを取得する
        /// </summary>
        /// <param name="sessionType"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public ITransactionService<IDisposable> GetTransactionService(Type sessionType)
        {
            if (!typeof(IDisposable).IsAssignableFrom(sessionType))
            {
                throw new InvalidOperationException("トランザクションセッションの型がIDisposableではありません");
            }
            return _serviceProvider.GetService(typeof(ITransactionService<>).MakeGenericType(sessionType)) as ITransactionService<IDisposable> ??
                throw new InvalidOperationException("管理されていないトランザクションサービスです");
        }
    }
}
