namespace CSStack.TADA
{
    /// <summary>
    /// 戻り値なしユースケースインターフェース
    /// </summary>
    /// <typeparam name="TReq">ユースケースの引数</typeparam>
    public interface IUseCaseWithoutRes<TReq> where TReq : IUseCaseDTO
    {
        /// <summary>
        /// ユースケースを実行する
        /// </summary>
        /// <param name="req"></param>
        ValueTask ExecuteAsync(TReq req);
    }
}