namespace CSStack.TADA
{
    /// <summary>
    /// ユースケースコマンドインターフェース
    /// </summary>
    /// <typeparam name="TReq">ユースケースの引数</typeparam>
    public interface ICommandService<TReq> where TReq : ICommandServiceDTO
    {
        /// <summary>
        /// ユースケースコマンドを実行する
        /// </summary>
        /// <param name="req"></param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns></returns>
        ValueTask ExecuteAsync(TReq req, CancellationToken cancellationToken);
    }
}