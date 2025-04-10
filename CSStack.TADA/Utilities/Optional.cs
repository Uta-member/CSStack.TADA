namespace CSStack.TADA
{
    /// <summary>
    /// 値が設定されたかどうかを管理するクラス
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    public class Optional<TValue>
    {
        private readonly TValue? _value;

        /// <summary>
        /// コンストラクタ（値未設定状態で生成する）
        /// </summary>
        public Optional()
        {
            HasValue = false;
            _value = default!;
        }

        /// <summary>
        /// コンストラクタ（値設定状態で生成する）
        /// </summary>
        /// <param name="value"></param>
        public Optional(TValue value)
        {
            HasValue = true;
            _value = value;
        }

        /// <summary>
        /// 値が設定されたかどうか
        /// </summary>
        public bool HasValue { get; }

        /// <summary>
        /// 値
        /// </summary>
        public TValue Value
        {
            get
            {
                if (!HasValue)
                {
                    throw new InvalidOperationException("Value is not set.");
                }

                return _value!;
            }
        }

        /// <summary>
        /// TValueをそのまま受け取ってインスタンス化できるようにする
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator Optional<TValue>(TValue value) => new Optional<TValue>(value);

        /// <inheritdoc/>
        public override string ToString()
        {
            return HasValue ? $"Some({Value})" : "None";
        }

        /// <summary>
        /// 値が設定済みなら取得する
        /// </summary>
        /// <param name="value">値</param>
        /// <returns>値が取得可能かどうか</returns>
        public bool TryGetValue(out TValue value)
        {
            if (HasValue)
            {
                value = Value;
            }
            else
            {
                value = default!;
            }
            return HasValue;
        }
    }
}
