﻿namespace CSStack.TADA
{
    /// <summary>
    /// ドメインサービス（引数あり・戻り値あり）のインターフェース
    /// </summary>
    /// <typeparam name="TReq">引数の型</typeparam>
    /// <typeparam name="TRes">戻り値の型</typeparam>
    public interface IDomainService<TReq, TRes> where TReq : IDomainServiceDTO where TRes : IDomainServiceDTO
    {
        /// <summary>
        /// 実行
        /// </summary>
        /// <param name="req">リクエスト</param>
        /// <returns>レスポンス</returns>
        ValueTask<TRes> ExecuteAsync(TReq req);
    }
}
