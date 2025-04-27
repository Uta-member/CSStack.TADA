namespace CSStack.TADA
{
    /// <summary>
    /// ユースケースコマンドインターフェース
    /// </summary>
    /// <typeparam name="TReq">引数</typeparam>
    /// <typeparam name="TRes">戻り値</typeparam>
    public interface ICommandServiceWithRes<TReq, TRes>
        where TReq : ICommandServiceDTO
        where TRes : ICommandServiceDTO
    {
        /// <summary>
        /// ユースケースコマンドを実行する
        /// </summary>
        /// <param name="req"></param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns></returns>
        ValueTask<TRes> ExecuteAsync(TReq req, CancellationToken cancellationToken = default);
    }
}
