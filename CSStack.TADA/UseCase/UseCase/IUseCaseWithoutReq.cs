namespace CSStack.TADA
{
    /// <summary>
    /// 引数なしのユースケースインターフェース
    /// </summary>
    /// <typeparam name="TRes">ユースケースの戻り値</typeparam>
    public interface IUseCaseWithoutReq<TRes> where TRes : IUseCaseDTO
    {
        /// <summary>
        /// ユースケースを実行する
        /// </summary>
        /// <returns></returns>
        ValueTask<TRes> ExecuteAsync();
    }
}