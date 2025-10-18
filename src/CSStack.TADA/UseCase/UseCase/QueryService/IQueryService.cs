namespace CSStack.TADA
{
    /// <summary>
    /// Query service interface.
    /// </summary>
    public interface IQueryService<TReq, TRes>
        where TReq : IQueryServiceDTO
        where TRes : IQueryServiceDTO
    {
        /// <summary>
        /// Execute the query service method.
        /// </summary>
        /// <param name="req">Request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Response</returns>
        ValueTask<TRes> ExecuteAsync(TReq req, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Query service interface.
    /// </summary>
    public interface IQueryService<TRes>
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
