namespace CSStack.TADA
{
	/// <summary>
	/// Exception thrown when an invalid operation is performed within the domain.
	/// </summary>
	/// <remarks>
	/// Constructor
	/// </remarks>
	/// <param name="message">Message</param>
	/// <param name="innerException">Inner exception</param>
	public class DomainInvalidOperationException(string? message = null, Exception? innerException = null)
		: TADAException(message, innerException)
	{
	}
}
