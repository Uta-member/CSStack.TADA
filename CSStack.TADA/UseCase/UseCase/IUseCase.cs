namespace CSStack.TADA
{
    /// <summary>
    /// ユースケースインターフェース
    /// </summary>
    /// <typeparam name="TReq">ユースケースの引数</typeparam>
    /// <typeparam name="TRes">ユースケースの戻り値</typeparam>
    public interface IUseCase<TReq, TRes> where TReq : IUseCaseDTO where TRes : IUseCaseDTO
    {
        /// <summary>
        /// ユースケースを実行する
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        ValueTask<TRes> ExecuteAsync(TReq req);
    }
}