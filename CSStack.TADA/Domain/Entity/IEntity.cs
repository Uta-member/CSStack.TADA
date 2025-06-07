namespace CSStack.TADA
{
    /// <summary>
    /// エンティティのインターフェース
    /// </summary>
    /// <typeparam name="TIdentifier">エンティティの識別子となるオブジェクトの型</typeparam>
    public interface IEntity<TIdentifier>
        where TIdentifier : notnull
    {
        /// <summary>
        /// バリデーション
        /// </summary>
        void Validate();

        /// <summary>
        /// Entityの識別子
        /// </summary>
        TIdentifier Identifier { get; }

        /// <summary>
        /// 不正値が入っているかどうか
        /// </summary>
        bool IsInvalidValue { get; }
    }
}
