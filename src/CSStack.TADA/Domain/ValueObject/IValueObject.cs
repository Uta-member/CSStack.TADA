namespace CSStack.TADA
{
	/// <summary>
	/// Interface for value objects. A marker: it declares intent and carries no members.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Implement value objects as a <c>record</c> — ideally <c>sealed record</c>.</b> A value object has
	/// no identity: two instances holding the same values are the same value, and the value must not change
	/// after construction. A record gives you both for free — compiler-generated value equality,
	/// <c>GetHashCode</c>, <c>ToString</c> and <c>with</c> — which is why this interface does not force
	/// them. Implementing it on a plain <c>class</c> leaves reference equality in place, so two objects
	/// holding the same value compare unequal; that is a bug this marker cannot catch.
	/// There is no <c>ValueObjectBase</c>: it was removed in v2.0.0 precisely because <c>record</c> does
	/// the job better.
	/// </para>
	/// <para>
	/// Prefer <see cref="ISingleValueObject{TValue, TSelf}"/> when the value object wraps a single
	/// primitive; it adds the <c>Create</c> / <c>Reconstruct</c> contract that keeps validation in one place.
	/// </para>
	/// <example>
	/// <code>
	/// public sealed record UserName : ISingleValueObject&lt;string, UserName&gt;
	/// {
	///     private UserName(string value) => Value = value;
	///
	///     public string Value { get; }
	///
	///     public static UserName Create(string value) =&gt; new(Validate(value));
	///
	///     public static UserName Reconstruct(string value) =&gt; new(value);
	///
	///     private static string Validate(string value) =&gt; /* throw ValueObjectInvalidException when invalid */ value;
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public interface IValueObject
	{
	}
}
