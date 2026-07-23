namespace CSStack.TADA.Sample
{
    /// <summary>
    /// 書き込みと一緒に記録する「誰が・いつ」。
    /// <c>IRepository</c> の <c>TOperateInfo</c> に相当する。
    /// </summary>
    /// <remarks>
    /// 読み取り系のメソッドはこれを受け取らない。読んでも何も変わらないので記録するものが無いため。
    /// </remarks>
    public sealed record OperateInfo(string OperatorId, DateTimeOffset OperatedAt)
    {
        /// <summary>
        /// 「今、この操作者が」を表す <see cref="OperateInfo"/> を作る。
        /// </summary>
        public static OperateInfo Now(string operatorId)
        {
            return new OperateInfo(operatorId, DateTimeOffset.UtcNow);
        }
    }
}
