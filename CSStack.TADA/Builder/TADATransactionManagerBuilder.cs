using Microsoft.Extensions.DependencyInjection;

namespace CSStack.TADA
{
    /// <summary>
    /// TADAのトランザクションに必要なサービスを登録するクラス
    /// </summary>
    public static class TADATransactionManagerBuilder
    {
        /// <summary>
        /// TADAのトランザクションに必要なサービスを登録する
        /// </summary>
        /// <typeparam name="TSessionIdentifier"></typeparam>
        /// <param name="services"></param>
        /// <param name="transactionTypeDictionary"></param>
        public static void AddTADATransactionManager<TSessionIdentifier>(
            this IServiceCollection services,
            Dictionary<TSessionIdentifier, Type> transactionTypeDictionary)
            where TSessionIdentifier : Enum
        {
            services.AddTransient<ITransactionTypeResolver<TSessionIdentifier>, TransactionTypeResolver<TSessionIdentifier>>(
                x => new TransactionTypeResolver<TSessionIdentifier>(transactionTypeDictionary));
            services.AddTransient<ITransactionManager<TSessionIdentifier>, TransactionManager<TSessionIdentifier>>();
            var distinctTransactionTypes = transactionTypeDictionary.DistinctBy(x => x.Value);
        }
    }
}
