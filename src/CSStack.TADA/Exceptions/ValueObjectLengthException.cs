namespace CSStack.TADA
{
	/// <summary>
	/// Exception related to the length of a value object.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Constructor
	/// </para>
	/// <para>
	/// <b>The three length arguments are all <c>int</c>, so getting the order wrong still compiles</b>
	/// and produces an exception that reports the wrong bounds. The order is
	/// <paramref name="minLength"/>, <paramref name="maxLength"/>, <paramref name="currentLength"/> —
	/// the two bounds declared by the value object first, the rejected length last. Prefer named arguments
	/// (<c>new ValueObjectLengthException(minLength: MinLength, maxLength: MaxLength,
	/// currentLength: value.Length)</c>) when the values are not obviously named at the call site.
	/// </para>
	/// </remarks>
	/// <param name="minLength">
	/// The shortest length the value object accepts, as declared by the value object itself — not the
	/// length of the value that was rejected. Exposed as <see cref="MinLength"/>.
	/// </param>
	/// <param name="maxLength">
	/// The longest length the value object accepts, as declared by the value object itself — not the
	/// length of the value that was rejected. Exposed as <see cref="MaxLength"/>.
	/// </param>
	/// <param name="currentLength">
	/// The length of the value that was rejected, which falls outside
	/// <paramref name="minLength"/>..<paramref name="maxLength"/>. Exposed as <see cref="CurrentLength"/>.
	/// </param>
	/// <param name="message">Message</param>
	/// <param name="innerException">Inner exception</param>
	public class ValueObjectLengthException(
		int minLength,
		int maxLength,
		int currentLength,
		string? message = null,
		Exception? innerException = null)
		: ValueObjectInvalidException(message, innerException)
	{
		/// <summary>
		/// The length of the value that was rejected.
		/// </summary>
		public int CurrentLength { get; } = currentLength;

		/// <summary>
		/// The longest length the value object accepts.
		/// </summary>
		public int MaxLength { get; } = maxLength;

		/// <summary>
		/// The shortest length the value object accepts.
		/// </summary>
		public int MinLength { get; } = minLength;
	}
}
