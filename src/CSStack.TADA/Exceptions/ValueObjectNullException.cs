namespace CSStack.TADA
{
	/// <summary>
	/// Exception thrown when a value object has no value.
	/// </summary>
	public class ValueObjectNullException : ValueObjectInvalidException
	{
		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="message">Message</param>
		/// <param name="innerException">Inner exception</param>
		public ValueObjectNullException(string? message = null, Exception? innerException = null)
			: base(message, innerException)
		{
		}
	}
}
