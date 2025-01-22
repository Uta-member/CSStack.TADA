namespace CSStack.TADA
{
    /// <summary>
    /// 値の長さが決まっている単一値オブジェクトのインターフェース
    /// </summary>
    public interface ILengthDefinedSingleValueObject
    {
        /// <summary>
        /// 最大長
        /// </summary>
        abstract static int MaxLength { get; }

        /// <summary>
        /// 最小長
        /// </summary>
        abstract static int MinLength { get; }
    }
}
