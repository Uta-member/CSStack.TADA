namespace CSStack.TADA
{
    /// <summary>
    /// クエリサービスインターフェース
    /// </summary>
    public interface IQueryServiceWithoutReq<TRes>
        where TRes : IQueryServiceDTO
    {
        /// <summary>
        /// クエリサービスのメソッドを実行する
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>戻り値</returns>
        ValueTask<TRes> ExecuteAsync(CancellationToken cancellationToken = default);
    }
}
