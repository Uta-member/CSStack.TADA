namespace CSStack.TADA
{
	/// <summary>
	/// Interface for value objects that hold a single value, without the <c>Create</c> / <c>Reconstruct</c>
	/// contract.
	/// </summary>
	/// <typeparam name="TValue">Value type</typeparam>
	/// <remarks>
	/// Use this one to accept "any value object wrapping a <typeparamref name="TValue"/>" in a generic
	/// constraint, where the static factory members of
	/// <see cref="ISingleValueObject{TValue, TSelf}"/> would force the concrete type to be named as well.
	/// When declaring a value object, prefer <see cref="ISingleValueObject{TValue, TSelf}"/>.
	/// </remarks>
	public interface ISingleValueObject<TValue> : IValueObject
	{
		/// <summary>
		/// Underlying value.
		/// </summary>
		TValue Value { get; }
	}

	/// <summary>
	/// Interface for value objects that hold a single value, with the <see cref="Create(TValue)"/> /
	/// <see cref="Reconstruct(TValue)"/> contract.
	/// </summary>
	/// <typeparam name="TValue">Value type</typeparam>
	/// <typeparam name="TSelf">Value object type. Pass the type that implements this interface.</typeparam>
	/// <remarks>
	/// <para>
	/// <b>Validation lives in <see cref="Create(TValue)"/>, and nowhere else.</b> There is no
	/// <c>Validate</c> member to implement — one existed and was removed, because being callable from the
	/// outside implied a value object could exist in an unvalidated state, which is exactly what a value
	/// object is supposed to rule out. Keep the constructor private so that
	/// <see cref="Create(TValue)"/> and <see cref="Reconstruct(TValue)"/> are the only ways in; then an
	/// instance that exists is an instance that passed.
	/// </para>
	/// <para>
	/// Implement value objects as a <c>record</c> so that equality is by value. See
	/// <see cref="IValueObject"/>.
	/// </para>
	/// </remarks>
	public interface ISingleValueObject<TValue, TSelf> : IValueObject where TSelf : ISingleValueObject<TValue, TSelf>
	{
		/// <summary>
		/// Underlying value.
		/// </summary>
		TValue Value { get; }

		/// <summary>
		/// Create a new instance from untrusted input, applying the invariants of this value object.
		/// </summary>
		/// <remarks>
		/// Reject invalid input by throwing <see cref="ValueObjectInvalidException"/> or a type derived
		/// from it — <see cref="ValueObjectNullException"/> when the value is missing,
		/// <see cref="ValueObjectLengthException"/> when it breaks the bounds declared by
		/// <see cref="ILengthDefinedSingleValueObject"/> — rather than returning a sentinel. Do not return
		/// an instance that failed validation.
		/// </remarks>
		/// <param name="value">Value</param>
		/// <returns>A validated instance</returns>
		/// <exception cref="ValueObjectInvalidException"><paramref name="value"/> breaks an invariant.</exception>
		static abstract TSelf Create(TValue value);

		/// <summary>
		/// Rebuild an instance from a value that has already been validated once and persisted.
		/// </summary>
		/// <remarks>
		/// <b>Skips validation on purpose.</b> It exists so that data written under older rules can still
		/// be read back after the rules were tightened, which <see cref="Create(TValue)"/> would refuse to
		/// do. Call it only from a repository restoring a persisted value; anything that comes from a user,
		/// an API or another system goes through <see cref="Create(TValue)"/>.
		/// </remarks>
		/// <param name="value">A value read back from the store</param>
		/// <returns>An instance holding <paramref name="value"/> as-is</returns>
		static abstract TSelf Reconstruct(TValue value);
	}
}
