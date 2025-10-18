namespace CSStack.TADA
{
    /// <summary>
    /// Use case command interface.
    /// </summary>
    /// <typeparam name="TReq">Use case request type</typeparam>
    public interface ICommandService<TReq>
        where TReq : ICommandServiceDTO
    {
        /// <summary>
        /// Execute the use case command.
        /// </summary>
        /// <param name="req">Request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        ValueTask ExecuteAsync(TReq req, CancellationToken cancellationToken = default);
    }


    /// <summary>
    /// Use case command interface.
    /// </summary>
    /// <typeparam name="TReq">Request type</typeparam>
    /// <typeparam name="TRes">Response type</typeparam>
    public interface ICommandService<TReq, TRes>
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