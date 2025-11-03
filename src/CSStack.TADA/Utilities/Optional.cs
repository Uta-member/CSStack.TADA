using System.Diagnostics.CodeAnalysis;

namespace CSStack.TADA
{
    /// <summary>
    /// Three-state struct that manages whether a value is set: None/Some(null)/Some(value). For reference types of
    /// <typeparamref name="TValue"/>, null can be stored (HasValue=true and Value=null).
    /// </summary>
    /// <typeparam name="TValue">Value type</typeparam>
    public struct Optional<TValue>
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
        /// Constructor that creates an instance with an explicit <see cref="HasValue"/> state. If <paramref
        /// name="hasValue"/> is true, the provided <paramref name="value"/> (which may be null for reference types) is
        /// stored and the instance represents Some(value). If false, the instance represents None and the stored value
        /// is ignored by accessors (for example, <see cref="Value"/> returns default and <see cref="TryGetValue(out
        /// TValue)"/> returns false).
        /// </summary>
        /// <param name="value">Value to associate with the instance (nullable for reference types)</param>
        /// <param name="hasValue">Whether the instance should represent a set value (Some) or not set (None)</param>
        public Optional(TValue? value, bool hasValue)
        {
            HasValue = hasValue;
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
        public bool TryGetValue(out TValue value)
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
        public bool HasValue { get; init; }

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
            init
            {
                _value = value;
            }
        }
    }
}
