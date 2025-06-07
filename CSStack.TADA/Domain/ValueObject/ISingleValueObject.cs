namespace CSStack.TADA
{
    /// <summary>
    /// 単一値しか持たない値オブジェクトインターフェース。
    /// </summary>
    /// <typeparam name="TValue">値の型</typeparam>
    /// <typeparam name="TSelf">値オブジェクトの型</typeparam>
    public interface ISingleValueObject<TValue, TSelf> : IValueObject
        where TSelf : ISingleValueObject<TValue, TSelf>
    {
        /// <summary>
        /// 新しく生成する
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        static abstract TSelf Create(TValue value);

        /// <summary>
        /// リポジトリから復元する
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        static abstract TSelf Reconstruct(TValue value);

        /// <summary>
        /// 値の本体
        /// </summary>
        TValue Value { get; }
    }
}
