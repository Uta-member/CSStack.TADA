namespace CSStack.TADA
{
    /// <summary>
    /// ドメインサービス（引数なし・戻り値あり）のインターフェース
    /// </summary>
    /// <typeparam name="TRes">戻り値の型</typeparam>
    public interface IDomainServiceWithoutReq<TRes> where TRes : IDomainServiceDTO
    {
        /// <summary>
        /// 実行
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>レスポンス</returns>
        ValueTask<TRes> ExecuteAsync(CancellationToken cancellationToken);
    }
}
