namespace CSStack.TADA
{
	/// <summary>
	/// Exception related to the length of a value object.
	/// </summary>
	/// <remarks>
	/// Constructor
	/// </remarks>
	/// <param name="minLength">Minimum length</param>
	/// <param name="maxLength">Maximum length</param>
	/// <param name="currentLength">Current length</param>
	/// <param name="message">Message</param>
	/// <param name="innserException">Inner exception</param>
	public class ValueObjectLengthException(
		int minLength,
		int maxLength,
		int currentLength,
		string? message = null,
		Exception? innserException = null)
		: ValueObjectInvalidException(message, innserException)
	{
		/// <summary>
		/// Current length
		/// </summary>
		public int CurrentLength { get; } = currentLength;

		/// <summary>
		/// Maximum length
		/// </summary>
		public int MaxLength { get; } = maxLength;

		/// <summary>
		/// Minimum length
		/// </summary>
		public int MinLength { get; } = minLength;
	}
}
