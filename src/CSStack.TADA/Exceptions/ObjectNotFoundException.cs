namespace CSStack.TADA
{
	/// <summary>
	/// Exception thrown when an operation required an object that does not exist.
	/// </summary>
	/// <remarks>
	/// <b>Repositories never throw this.</b>
	/// <see cref="IRepository{TEntity, TEntityIdentifier, TOperateInfo, TSession}.FindByIdentifierAsync"/>
	/// reports absence as <see cref="Optional{TValue}.Empty"/>, because not finding something is a normal
	/// result and only the operation being carried out knows whether it is a problem — deleting an already
	/// deleted entity may be fine, charging a missing account is not. The layer that turns
	/// <see cref="Optional{TValue}.Empty"/> into this exception is therefore the aggregate service or the
	/// use case that needed the object.
	/// </remarks>
	public class ObjectNotFoundException : TADAException
	{
		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="message">Message</param>
		/// <param name="innserException">Inner exception</param>
		public ObjectNotFoundException(string? message = null, Exception? innserException = null)
			: base(message, innserException)
		{
		}
	}
}
