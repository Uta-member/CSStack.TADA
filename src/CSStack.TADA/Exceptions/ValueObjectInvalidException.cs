namespace CSStack.TADA
{
	/// <summary>
	/// Exception class for invalid value objects.
	/// </summary>
	public class ValueObjectInvalidException : TADAException
	{
		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="message">Message</param>
		/// <param name="innerException">Inner exception</param>
		public ValueObjectInvalidException(string? message = null, Exception? innerException = null)
			: base(message, innerException)
		{
		}
	}
}
