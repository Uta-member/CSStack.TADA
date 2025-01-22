namespace CSStack.TADA
{
    /// <summary>
    /// ドメインサービス（引数あり・戻り値なし）のインターフェース
    /// </summary>
    /// <typeparam name="TReq">引数の型</typeparam>
    public interface IDomainServiceWithoutRes<TReq> where TReq : IDomainServiceDTO
    {
        /// <summary>
        /// 実行
        /// </summary>
        /// <param name="req">リクエスト</param>
        ValueTask ExecuteAsync(TReq req);
    }
}
