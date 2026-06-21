namespace CSStack.TADA
{
	/// <summary>
	/// Exception thrown when an invalid operation is performed within the domain.
	/// </summary>
	/// <remarks>
	/// Constructor
	/// </remarks>
	/// <param name="message">Message</param>
	/// <param name="innserException">Inner exception</param>
	public class DomainInvalidOperationException(string? message = null, Exception? innserException = null)
		: TADAException(message, innserException)
	{
	}
}
