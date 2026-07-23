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
		/// <param name="innerException">Inner exception</param>
		public ObjectAlreadyExistException(string? message = null, Exception? innerException = null)
			: base(message, innerException)
		{
		}

		/// <summary>
		/// Constructor that records what was already there. Prefer this overload: the type and the identifier
		/// end up on the exception itself, so a log entry says which object collided without the message
		/// having to be written by hand at every call site.
		/// </summary>
		/// <param name="objectType">The type of the object that already exists, usually the entity type</param>
		/// <param name="identifier">
		/// The identifier the object was looked up by — often the value that has to be unique rather than the
		/// primary key, such as the e-mail address that is already taken. <see langword="null"/> when the
		/// lookup was not by identifier. Its <see cref="object.ToString"/> goes into the generated message, so
		/// pass something that reads well in a log — and nothing secret.
		/// </param>
		/// <param name="message">Message. A message is generated from the arguments when this is
		/// <see langword="null"/>.</param>
		/// <param name="innerException">Inner exception</param>
		/// <exception cref="ArgumentNullException"><paramref name="objectType"/> is <see langword="null"/>.</exception>
		public ObjectAlreadyExistException(
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
		/// The type of the object that already exists.
		/// <see langword="null"/> when the exception was created from a raw message.
		/// </summary>
		public Type? ObjectType { get; }

		private static string BuildMessage(Type objectType, object? identifier)
		{
			ArgumentNullException.ThrowIfNull(objectType);
			return identifier is null
				? $"The object of type '{objectType.FullName}' already exists."
				: $"The object of type '{objectType.FullName}' identified by '{identifier}' already exists.";
		}
	}
}
