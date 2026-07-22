namespace CSStack.TADA
{
	/// <summary>
	/// Exception thrown when an operation required an object not to exist, and it did.
	/// </summary>
	/// <remarks>
	/// <b>Repositories never throw this.</b>
	/// <see cref="IRepository{TEntity, TEntityIdentifier, TOperateInfo, TSession}.SaveAsync"/> is an upsert,
	/// so an existing record is not a failure there. Throw this from the aggregate service or use case that
	/// looked the object up first and found one where there had to be none — registering a user against an
	/// e-mail address that is already taken, for instance.
	/// </remarks>
	public class ObjectAlreadyExistException : TADAException
	{
		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="message">Message</param>
		/// <param name="innserException">Inner exception</param>
		public ObjectAlreadyExistException(string? message = null, Exception? innserException = null)
			: base(message, innserException)
		{
		}
	}
}
