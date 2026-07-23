namespace CSStack.TADA.Sample
{
    /// <summary>
    /// ユーザー名。長さの制約を持つ単一値オブジェクト。
    /// </summary>
    /// <remarks>
    /// <see cref="ILengthDefinedSingleValueObject"/> は長さの上下限を<b>公開する</b>だけで、強制はしない。
    /// 強制するのは <see cref="Create"/>。プレゼンテーション層は
    /// <c>UserName.MaxLength</c> をそのまま入力欄の <c>maxlength</c> に使えるので、
    /// 同じ数字を 2 箇所に書かずに済む。
    /// </remarks>
    public sealed record UserName
        : ISingleValueObject<string, UserName>, ISingleValueObject<string>, ILengthDefinedSingleValueObject
    {
        private UserName(string value)
        {
            Value = value;
        }

        /// <summary>
        /// 受け付ける最大長（この値を含む）。
        /// </summary>
        public static int MaxLength => 16;

        /// <summary>
        /// 受け付ける最小長（この値を含む）。
        /// </summary>
        public static int MinLength => 1;

        /// <summary>
        /// 中身の値。
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// 外部からの入力から生成する。検証はここに書く。
        /// </summary>
        /// <exception cref="ValueObjectNullException"><paramref name="value"/> が null。</exception>
        /// <exception cref="ValueObjectLengthException">長さが <see cref="MinLength"/>〜<see cref="MaxLength"/> の外。</exception>
        public static UserName Create(string value)
        {
            if (value is null)
            {
                throw new ValueObjectNullException($"{nameof(UserName)} に null は指定できません。");
            }
            if (value.Length < MinLength || value.Length > MaxLength)
            {
                // 引数が 3 つとも int なので、順番を間違えてもコンパイルは通る。名前付き引数で渡す。
                throw new ValueObjectLengthException(
                    minLength: MinLength,
                    maxLength: MaxLength,
                    currentLength: value.Length);
            }

            return new UserName(value);
        }

        /// <summary>
        /// 永続化された値から復元する。<b>検証しない。</b>
        /// </summary>
        public static UserName Reconstruct(string value)
        {
            return new UserName(value);
        }

        /// <summary>
        /// ログや画面に出すための表現。
        /// </summary>
        public override string ToString()
        {
            return Value;
        }
    }
}
