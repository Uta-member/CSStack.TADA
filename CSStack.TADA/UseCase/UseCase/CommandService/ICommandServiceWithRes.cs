using System.ComponentModel;

namespace CSStack.TADA
{
    /// <summary>
    /// Use case command interface.
    /// </summary>
    /// <typeparam name="TReq">Request type</typeparam>
    /// <typeparam name="TRes">Response type</typeparam>
    [Obsolete(
        "ICommandServiceWithRes<TReq, TRes> is obsolete and will be removed in a future version. Use ICommandService<TReq, TRes> instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface ICommandServiceWithRes<TReq, TRes>
        where TReq : ICommandServiceDTO
        where TRes : ICommandServiceDTO
    {
        /// <summary>
        /// Execute the use case command.
        /// </summary>
        /// <param name="req">Request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Response</returns>
        ValueTask<TRes> ExecuteAsync(TReq req, CancellationToken cancellationToken = default);
    }
}
