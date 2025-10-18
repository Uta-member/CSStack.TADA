using System.ComponentModel;

namespace CSStack.TADA
{
    /// <summary>
    /// Query service interface.
    /// </summary>
    [Obsolete(
        "IQueryServiceWithoutReq<TRes> is obsolete and will be removed in a future version. Use IQueryService<TRes> instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IQueryServiceWithoutReq<TRes>
        where TRes : IQueryServiceDTO
    {
        /// <summary>
        /// Execute the query service method.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Response</returns>
        ValueTask<TRes> ExecuteAsync(CancellationToken cancellationToken = default);
    }
}
