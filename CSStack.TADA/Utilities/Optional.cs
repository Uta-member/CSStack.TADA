using System.Diagnostics.CodeAnalysis;

namespace CSStack.TADA
{
    /// <summary>
    /// Three-state struct that manages whether a value is set: None/Some(null)/Some(value). For reference types of
    /// <typeparamref name="TValue"/>, null can be stored (HasValue=true and Value=null).
    /// </summary>
    /// <typeparam name="TValue">Value type</typeparam>
    public readonly struct Optional<TValue>
    {
        private readonly TValue? _value;

        /// <summary>
        /// Constructor that creates an instance in the "not set" (None) state.
        /// </summary>
        public Optional()
        {
            HasValue = false;
            _value = default;
        }

        /// <summary>
        /// Constructor that creates an instance with a value set (Some).
        /// </summary>
        /// <param name="value">Value to store (null allowed for reference types)</param>
        public Optional(TValue value)
        {
            HasValue = true;
            _value = value;
        }

        /// <summary>
        /// Allows implicit construction from <typeparamref name="TValue"/> (null is accepted as Some(null)).
        /// </summary>
        /// <param name="value">Value</param>
        public static implicit operator Optional<TValue>(TValue value)
        {
            return new Optional<TValue>(value);
        }

        /// <summary>
        /// Returns the stored value when set; otherwise returns the specified default value.
        /// </summary>
        /// <param name="defaultValue">Value to return when no value is set</param>
        /// <returns></returns>
        public TValue GetValue(TValue defaultValue)
        {
            return TryGetValue(out var value) ? value : defaultValue;
        }

        /// <summary>
        /// Creates an Optional instance in the Some state.
        /// </summary>
        /// <param name="value">Value</param>
        /// <returns></returns>
        public static Optional<TValue> Some(TValue value)
        {
            return new Optional<TValue>(value);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if(!HasValue)
            {
                return "None";
            }

            return $"Some({(_value is null ? "null" : _value)})";
        }

        /// <summary>
        /// Gets the value if it is set.
        /// </summary>
        /// <param name="value">Value</param>
        /// <returns>Whether the value was available</returns>
        public bool TryGetValue([MaybeNullWhen(false)] out TValue value)
        {
            if(HasValue)
            {
                value = _value!;
                return true;
            }

            value = default!;
            return false;
        }

        /// <summary>
        /// Gets an Optional instance in the None state.
        /// </summary>
        public static Optional<TValue> Empty => default;

        /// <summary>
        /// Indicates whether a value is set.
        /// </summary>
        public bool HasValue { get; }

        /// <summary>
        /// Value.
        /// </summary>
        public TValue? Value
        {
            get
            {
                if(!HasValue)
                {
                    return default;
                }

                return _value!;
            }
        }
    }
}
