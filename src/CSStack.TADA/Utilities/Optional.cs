using System.Diagnostics.CodeAnalysis;

namespace CSStack.TADA
{
	/// <summary>
	/// Three-state readonly struct that manages whether a value is set: <c>None</c> / <c>Some(null)</c> /
	/// <c>Some(value)</c>. For reference types of <typeparamref name="TValue"/>, null can be stored (<see
	/// cref="HasValue"/> is true and <see cref="Value"/> is null).
	/// </summary>
	/// <remarks>
	/// <para>
	/// The third state exists so that "not specified" can be distinguished from "explicitly set to null", which
	/// is required for partial updates such as HTTP PATCH. If you only need "set or not set", this type behaves
	/// like <see cref="Nullable{T}"/>.
	/// </para>
	/// <para>
	/// <b>Declare the nullability on the type argument.</b> Use <c>Optional&lt;string?&gt;</c> when null is a
	/// legal value and <c>Optional&lt;User&gt;</c> when it is not. The compiler's nullable analysis of <see
	/// cref="Value"/> and <see cref="TryGetValue(out TValue)"/> follows the type argument, so declaring it
	/// correctly is what makes <c>Some(null)</c> visible to the compiler.
	/// </para>
	/// <para>
	/// <b>Trap:</b> the implicit conversion from <typeparamref name="TValue"/> produces <c>Some(null)</c> for a
	/// null input, <b>not</b> <see cref="Empty"/>. Returning <c>null</c> to mean "not found" therefore yields an
	/// instance whose <see cref="HasValue"/> is true and whose <see cref="Value"/> is null. Return <see
	/// cref="Empty"/> instead.
	/// </para>
	/// <para>
	/// Equality distinguishes all three states: <c>None == None</c> is true, <c>Some(null) == Some(null)</c> is
	/// true, and <c>None == Some(null)</c> is <b>false</b>.
	/// </para>
	/// </remarks>
	/// <typeparam name="TValue">Value type</typeparam>
	public readonly struct Optional<TValue> : IEquatable<Optional<TValue>>
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
		/// <remarks>
		/// Passing null produces <c>Some(null)</c>, not <see cref="Empty"/>.
		/// </remarks>
		/// <param name="value">Value to store (null allowed for reference types)</param>
		public Optional(TValue value)
		{
			HasValue = true;
			_value = value;
		}

		/// <summary>
		/// Constructor that creates an instance with an explicit <see cref="HasValue"/> state. If <paramref
		/// name="hasValue"/> is true, the provided <paramref name="value"/> (which may be null for reference
		/// types) is stored and the instance represents <c>Some(value)</c>. If false, the instance represents
		/// <c>None</c> and <paramref name="value"/> is discarded.
		/// </summary>
		/// <param name="value">Value to associate with the instance (nullable for reference types)</param>
		/// <param name="hasValue">Whether the instance should represent a set value (Some) or not set (None)</param>
		public Optional(TValue? value, bool hasValue)
		{
			HasValue = hasValue;
			_value = hasValue ? value : default;
		}

		/// <summary>
		/// Gets an Optional instance in the None state. This is the value to return when a lookup found nothing.
		/// </summary>
		public static Optional<TValue> Empty => default;

		/// <summary>
		/// Indicates whether a value is set. True for both <c>Some(value)</c> and <c>Some(null)</c>.
		/// </summary>
		public bool HasValue { get; }

		/// <summary>
		/// Value. Returns <c>default</c> when <see cref="HasValue"/> is false, and may also be null when <see
		/// cref="HasValue"/> is true (the <c>Some(null)</c> state), so this property alone cannot be used to
		/// tell the two apart. Check <see cref="HasValue"/> or call <see cref="TryGetValue(out TValue)"/>.
		/// </summary>
		public TValue? Value => HasValue ? _value : default;

		/// <summary>
		/// Determines whether two instances represent the same state and value.
		/// </summary>
		/// <param name="left">Left operand</param>
		/// <param name="right">Right operand</param>
		/// <returns>True when both are <c>None</c>, or both are <c>Some</c> holding equal values</returns>
		public static bool operator ==(Optional<TValue> left, Optional<TValue> right)
		{
			return left.Equals(right);
		}

		/// <summary>
		/// Allows implicit construction from <typeparamref name="TValue"/>.
		/// </summary>
		/// <remarks>
		/// <b>null is accepted and produces <c>Some(null)</c>, not <see cref="Empty"/>.</b> Writing <c>return
		/// null;</c> in a method that returns <c>Optional&lt;T&gt;</c> therefore creates an instance whose <see
		/// cref="HasValue"/> is true, and <see cref="TryGetValue(out TValue)"/> will return true with a null
		/// out value. To represent "no value", return <see cref="Empty"/>.
		/// </remarks>
		/// <param name="value">Value</param>
		public static implicit operator Optional<TValue>(TValue value)
		{
			return new Optional<TValue>(value);
		}

		/// <summary>
		/// Determines whether two instances represent different states or values.
		/// </summary>
		/// <param name="left">Left operand</param>
		/// <param name="right">Right operand</param>
		/// <returns>True when the instances are not equal</returns>
		public static bool operator !=(Optional<TValue> left, Optional<TValue> right)
		{
			return !left.Equals(right);
		}

		/// <summary>
		/// Creates an Optional instance in the Some state.
		/// </summary>
		/// <remarks>
		/// Passing null produces <c>Some(null)</c>. Use <see cref="Empty"/> for the None state.
		/// </remarks>
		/// <param name="value">Value</param>
		/// <returns><c>Some(value)</c></returns>
		public static Optional<TValue> Some(TValue value)
		{
			return new Optional<TValue>(value);
		}

		/// <inheritdoc/>
		public override bool Equals(object? obj)
		{
			return obj is Optional<TValue> other && Equals(other);
		}

		/// <summary>
		/// Determines whether this instance represents the same state and value as <paramref name="other"/>.
		/// <c>None</c> equals only <c>None</c>; in particular <c>None</c> and <c>Some(null)</c> are not equal.
		/// </summary>
		/// <param name="other">Instance to compare with</param>
		/// <returns>True when both are <c>None</c>, or both are <c>Some</c> holding equal values</returns>
		public bool Equals(Optional<TValue> other)
		{
			if (HasValue != other.HasValue)
			{
				return false;
			}

			if (!HasValue)
			{
				return true;
			}

			return EqualityComparer<TValue?>.Default.Equals(_value, other._value);
		}

		/// <inheritdoc/>
		public override int GetHashCode()
		{
			return HashCode.Combine(HasValue, _value);
		}

		/// <summary>
		/// Returns the stored value when set; otherwise returns the specified default value.
		/// </summary>
		/// <remarks>
		/// <c>Some(null)</c> is a set value, so null is returned in that case rather than <paramref
		/// name="defaultValue"/>. <see cref="GetValueOrDefault(TValue)"/> is the same method under the
		/// conventional name.
		/// </remarks>
		/// <param name="defaultValue">Value to return when no value is set</param>
		/// <returns>The stored value, or <paramref name="defaultValue"/> when in the None state</returns>
		public TValue GetValue(TValue defaultValue)
		{
			return HasValue ? _value! : defaultValue;
		}

		/// <summary>
		/// Returns the stored value when set; otherwise returns the specified default value. Conventional alias
		/// of <see cref="GetValue(TValue)"/>.
		/// </summary>
		/// <remarks>
		/// <c>Some(null)</c> is a set value, so null is returned in that case rather than <paramref
		/// name="defaultValue"/>.
		/// </remarks>
		/// <param name="defaultValue">Value to return when no value is set</param>
		/// <returns>The stored value, or <paramref name="defaultValue"/> when in the None state</returns>
		public TValue GetValueOrDefault(TValue defaultValue)
		{
			return GetValue(defaultValue);
		}

		/// <summary>
		/// Runs one of two actions depending on whether a value is set.
		/// </summary>
		/// <param name="onSome">Action invoked with the stored value when in the Some state (the value may be
		/// null in the <c>Some(null)</c> state)</param>
		/// <param name="onNone">Action invoked when in the None state</param>
		/// <exception cref="ArgumentNullException"><paramref name="onSome"/> or <paramref name="onNone"/> is null.</exception>
		public void Match(Action<TValue> onSome, Action onNone)
		{
			ArgumentNullException.ThrowIfNull(onSome);
			ArgumentNullException.ThrowIfNull(onNone);

			if (HasValue)
			{
				onSome.Invoke(_value!);
				return;
			}

			onNone.Invoke();
		}

		/// <summary>
		/// Produces a result from one of two functions depending on whether a value is set. This is the
		/// exhaustive way to consume an <see cref="Optional{TValue}"/>.
		/// </summary>
		/// <typeparam name="TResult">Type of the produced result</typeparam>
		/// <param name="onSome">Function invoked with the stored value when in the Some state (the value may be
		/// null in the <c>Some(null)</c> state)</param>
		/// <param name="onNone">Function invoked when in the None state</param>
		/// <returns>The result of whichever function was invoked</returns>
		/// <exception cref="ArgumentNullException"><paramref name="onSome"/> or <paramref name="onNone"/> is null.</exception>
		public TResult Match<TResult>(Func<TValue, TResult> onSome, Func<TResult> onNone)
		{
			ArgumentNullException.ThrowIfNull(onSome);
			ArgumentNullException.ThrowIfNull(onNone);

			return HasValue ? onSome.Invoke(_value!) : onNone.Invoke();
		}

		/// <inheritdoc/>
		public override string ToString()
		{
			if (!HasValue)
			{
				return "None";
			}

			return $"Some({(_value is null ? "null" : _value)})";
		}

		/// <summary>
		/// Gets the value if it is set.
		/// </summary>
		/// <remarks>
		/// Returns true for <c>Some(null)</c> as well, in which case <paramref name="value"/> is null. When
		/// <typeparamref name="TValue"/> is declared as a nullable reference type (<c>Optional&lt;string?&gt;</c>)
		/// the compiler warns about dereferencing <paramref name="value"/>; when it is declared non-nullable
		/// (<c>Optional&lt;User&gt;</c>) a <c>Some(null)</c> instance violates that declaration and no warning
		/// is possible.
		/// </remarks>
		/// <param name="value">Value</param>
		/// <returns>Whether the value was available</returns>
		public bool TryGetValue([MaybeNullWhen(false)] out TValue value)
		{
			if (HasValue)
			{
				value = _value!;
				return true;
			}

			value = default;
			return false;
		}
	}
}
