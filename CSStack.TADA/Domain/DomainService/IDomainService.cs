﻿namespace CSStack.TADA
{
    /// <summary>
    /// ドメインサービス（引数あり・戻り値あり）のインターフェース
    /// </summary>
    /// <typeparam name="TReq">引数の型</typeparam>
    public interface IDomainService<TReq>
        where TReq : IDomainServiceDTO
    {
        /// <summary>
        /// 実行
        /// </summary>
        /// <param name="req">リクエスト</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        ValueTask ExecuteAsync(TReq req, CancellationToken cancellationToken = default);
    }
}
