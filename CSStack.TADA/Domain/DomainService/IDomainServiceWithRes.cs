using System.ComponentModel;

namespace CSStack.TADA
{
    /// <summary>
    /// Domain service interface (with request and response).
    /// </summary>
    /// <typeparam name="TReq">Request type</typeparam>
    /// <typeparam name="TRes">Response type</typeparam>
    [Obsolete(
        "IDomainServiceWithRes<TReq, TRes> is obsolete and will be removed in a future version. Use IDomainService<TReq, TRes> instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IDomainServiceWithRes<TReq, TRes>
        where TReq : IDomainServiceDTO
        where TRes : IDomainServiceDTO
    {
        /// <summary>
        /// Execute
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="req">Request</param>
        ValueTask<TRes> ExecuteAsync(TReq req, CancellationToken cancellationToken = default);
    }
}
