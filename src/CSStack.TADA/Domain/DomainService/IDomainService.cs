namespace CSStack.TADA
{
    /// <summary>
    /// Domain service interface (with request).
    /// </summary>
    /// <typeparam name="TReq">Request type</typeparam>
    public interface IDomainService<TReq>
        where TReq : IDomainServiceDTO
    {
        /// <summary>
        /// Execute.
        /// </summary>
        /// <param name="req">Request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        ValueTask ExecuteAsync(TReq req, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Domain service interface (with request and response).
    /// </summary>
    /// <typeparam name="TReq">Request type</typeparam>
    /// <typeparam name="TRes">Response type</typeparam>
    public interface IDomainService<TReq, TRes>
        where TReq : IDomainServiceDTO
        where TRes : IDomainServiceDTO
    {
        /// <summary>
        /// Execute.
        /// </summary>
        /// <param name="req">Request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        ValueTask<TRes> ExecuteAsync(TReq req, CancellationToken cancellationToken = default);
    }
}
