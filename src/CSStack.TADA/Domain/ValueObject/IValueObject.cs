namespace CSStack.TADA
{
	/// <summary>
	/// Interface for value objects. Carries a single member, <see cref="Validate"/>; otherwise it is a
	/// marker that declares intent.
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
	///     public static UserName Create(string value) =&gt; new(CheckInvariants(value));
	///
	///     public static UserName Reconstruct(string value) =&gt; new(value);
	///
	///     public void Validate() =&gt; CheckInvariants(Value);
	///
	///     private static string CheckInvariants(string value) =&gt; /* throw when invalid */ value;
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public interface IValueObject
	{
		/// <summary>
		/// Check the invariants of this value object and throw when one is broken.
		/// </summary>
		/// <remarks>
		/// <para>
		/// <b>This does not replace validation in <c>Create</c>.</b> An instance built through
		/// <c>Create</c> already passed once, so nothing calls this automatically. It exists because there
		/// is no way for a caller to verify from the outside whether a given instance actually went through
		/// <c>Create</c>. It is not tied to any particular scenario such as <c>Reconstruct</c> — it is a
		/// plain primitive that answers one question: does the value, as it stands right now, satisfy
		/// today's invariants?
		/// </para>
		/// <para>
		/// <b>Throw on failure; do not return a result.</b> This interface deliberately fixes the shape at
		/// <see langword="void"/> so that every value object exposes the same member regardless of how many
		/// call sites want a <c>bool</c> or a result type instead — those callers can wrap this in a
		/// <c>try</c>/<c>catch</c> of their own. Nothing here can force the exception type: this library no
		/// longer ships one for that purpose, so throw whatever fits the caller.
		/// </para>
		/// </remarks>
		void Validate();
	}
}
