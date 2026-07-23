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
		/// <param name="innerException">Inner exception</param>
		public ObjectNotFoundException(string? message = null, Exception? innerException = null)
			: base(message, innerException)
		{
		}

		/// <summary>
		/// Constructor that records what was looked up. Prefer this overload: the type and the identifier
		/// end up on the exception itself, so a log entry says which object was missing without the message
		/// having to be written by hand at every call site.
		/// </summary>
		/// <param name="objectType">The type of the object that was not found, usually the entity type</param>
		/// <param name="identifier">
		/// The identifier the object was looked up by, or <see langword="null"/> when the lookup was not by
		/// identifier. Its <see cref="object.ToString"/> goes into the generated message, so pass something
		/// that reads well in a log — and nothing secret.
		/// </param>
		/// <param name="message">Message. A message is generated from the arguments when this is
		/// <see langword="null"/>.</param>
		/// <param name="innerException">Inner exception</param>
		/// <exception cref="ArgumentNullException"><paramref name="objectType"/> is <see langword="null"/>.</exception>
		public ObjectNotFoundException(
			Type objectType,
			object? identifier,
			string? message = null,
			Exception? innerException = null)
			: base(message ?? BuildMessage(objectType, identifier), innerException)
		{
			ArgumentNullException.ThrowIfNull(objectType);
			Identifier = identifier;
			ObjectType = objectType;
		}

		/// <summary>
		/// The identifier the object was looked up by.
		/// <see langword="null"/> when the exception was created without one.
		/// </summary>
		public object? Identifier { get; }

		/// <summary>
		/// The type of the object that was not found.
		/// <see langword="null"/> when the exception was created from a raw message.
		/// </summary>
		public Type? ObjectType { get; }

		private static string BuildMessage(Type objectType, object? identifier)
		{
			ArgumentNullException.ThrowIfNull(objectType);
			return identifier is null
				? $"The object of type '{objectType.FullName}' was not found."
				: $"The object of type '{objectType.FullName}' identified by '{identifier}' was not found.";
		}
	}
}
