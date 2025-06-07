namespace CSStack.TADA
{
    /// <summary>
    /// クエリサービスインターフェース
    /// </summary>
    public interface IQueryService<TReq, TRes>
        where TReq : IQueryServiceDTO
        where TRes : IQueryServiceDTO
    {
        /// <summary>
        /// クエリサービスのメソッドを実行する
        /// </summary>
        /// <param name="req">リクエスト</param>
        /// <returns>戻り値</returns>
        ValueTask<TRes> ExecuteAsync(TReq req);
    }
}
