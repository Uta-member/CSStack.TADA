namespace CSStack.TADA
{
	/// <summary>
	/// Declares the length bounds of a value object, so that they can be read without constructing one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This interface only <i>publishes</i> the bounds; it does not enforce them. Enforcing them is the job
	/// of <see cref="ISingleValueObject{TValue, TSelf}.Create(TValue)"/>, which throws
	/// <see cref="ValueObjectLengthException"/> when the value falls outside. It is deliberately separate
	/// from <see cref="ISingleValueObject{TValue, TSelf}"/> and has no type parameters, so that anything
	/// with a length can implement it and so that the bounds stay reachable through a
	/// <c>where T : ILengthDefinedSingleValueObject</c> constraint — a presentation layer can then render
	/// <c>maxlength</c> from the same numbers the domain validates against, instead of restating them.
	/// </para>
	/// <example>
	/// <code>
	/// public sealed record UserName : ISingleValueObject&lt;string, UserName&gt;, ILengthDefinedSingleValueObject
	/// {
	///     private UserName(string value) => Value = value;
	///
	///     public static int MaxLength => 32;
	///     public static int MinLength => 1;
	///
	///     public string Value { get; }
	///
	///     public static UserName Create(string value)
	///     {
	///         if (value is null)
	///         {
	///             throw new ValueObjectNullException($"{nameof(UserName)} must not be null.");
	///         }
	///         if (value.Length &lt; MinLength || value.Length &gt; MaxLength)
	///         {
	///             throw new ValueObjectLengthException(MinLength, MaxLength, value.Length);
	///         }
	///
	///         return new UserName(value);
	///     }
	///
	///     public static UserName Reconstruct(string value) =&gt; new(value);
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public interface ILengthDefinedSingleValueObject
	{
		/// <summary>
		/// Maximum length, inclusive.
		/// </summary>
		static abstract int MaxLength { get; }

		/// <summary>
		/// Minimum length, inclusive.
		/// </summary>
		static abstract int MinLength { get; }
	}
}
