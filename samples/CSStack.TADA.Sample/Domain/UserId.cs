namespace CSStack.TADA.Sample
{
    /// <summary>
    /// ユーザーの識別子。値オブジェクトなので <c>record</c> で実装する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// コンストラクタを private にして <see cref="Create"/> / <see cref="Reconstruct"/> だけを入口にすると、
    /// 「存在しているインスタンスは検証を通ったインスタンス」が保証される。
    /// </para>
    /// <para>
    /// 型引数 1 個の <c>ISingleValueObject&lt;Guid&gt;</c> も併せて実装しておくと、
    /// <c>OptionalExtensions.ExchangeValueObjectToPrimitive</c> で中身を取り出せる。
    /// </para>
    /// </remarks>
    public sealed record UserId : ISingleValueObject<Guid, UserId>, ISingleValueObject<Guid>
    {
        private UserId(Guid value)
        {
            Value = value;
        }

        /// <summary>
        /// 中身の値。
        /// </summary>
        public Guid Value { get; }

        /// <summary>
        /// 新しい識別子を採番する。
        /// </summary>
        public static UserId New()
        {
            return Create(Guid.NewGuid());
        }

        /// <summary>
        /// 外部からの入力から生成する。検証はここに書く。
        /// </summary>
        /// <exception cref="ValueObjectInvalidException">空の <see cref="Guid"/> が渡された。</exception>
        public static UserId Create(Guid value)
        {
            // 検証は Create の中だけ。Validate メンバーは存在しない。
            if (value == Guid.Empty)
            {
                throw new ValueObjectInvalidException($"{nameof(UserId)} に空の Guid は指定できません。");
            }

            return new UserId(value);
        }

        /// <summary>
        /// 永続化された値から復元する。<b>検証しない。</b>
        /// </summary>
        /// <remarks>
        /// 呼んでよいのはリポジトリだけ。ユーザー入力や外部 API の値は <see cref="Create"/> を通す。
        /// 検証しないのは、ルールを厳しくした後でも古いデータを読み戻せるようにするため。
        /// </remarks>
        public static UserId Reconstruct(Guid value)
        {
            return new UserId(value);
        }

        /// <summary>
        /// ログや画面に出すための表現。
        /// </summary>
        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
