namespace CSStack.TADA
{
	/// <summary>
	/// Entity interface.
	/// </summary>
	/// <typeparam name="TIdentifier">Entity identifier type</typeparam>
	/// <remarks>
	/// <para>
	/// An entity is the one object of an aggregate that has an identity: it keeps being the same entity
	/// while its contents change, and two entities are the same when their <see cref="Identifier"/>s are
	/// equal. That is the whole contract — this interface deliberately requires nothing else.
	/// Inherit <see cref="EntityBase{TSelf, TIdentifier}"/> to get that equality implemented; implement
	/// this interface directly only when you need a different base class, and then implement identity
	/// equality yourself.
	/// </para>
	/// <para>
	/// A strongly-typed identifier — a value object such as <c>UserId</c> rather than a bare
	/// <see cref="Guid"/> — is recommended, so that an identifier of the wrong aggregate cannot be passed
	/// to <see cref="IRepository{TEntity, TEntityIdentifier, TOperateInfo, TSession}.FindByIdentifierAsync"/>.
	/// </para>
	/// <para>
	/// How an entity is constructed is not part of the contract. The recommended shape is a static
	/// <c>Create</c> that applies the invariants of a new entity and a static <c>Reconstruct</c> that
	/// rebuilds an already-persisted one without re-applying them, mirroring
	/// <see cref="ISingleValueObject{TValue, TSelf}"/>, but neither is enforced.
	/// </para>
	/// </remarks>
	public interface IEntity<TIdentifier> where TIdentifier : notnull
	{
		/// <summary>
		/// Entity identifier. Stable for the whole life of the entity; it is what equality is based on.
		/// </summary>
		TIdentifier Identifier { get; }

		/// <summary>
		/// Check the invariants of this entity and throw when one is broken.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Unlike a value object, an entity's state can change one field at a time over several method
		/// calls, so a rule can span more than one of them — call this after such a change to check the
		/// result as a whole, or after <c>Reconstruct</c> rebuilds an entity from data written under looser
		/// rules.
		/// </para>
		/// <para>
		/// <b>Throw on failure; do not return a result.</b> This interface deliberately fixes the shape at
		/// <see langword="void"/> so that every entity exposes the same member regardless of how many call
		/// sites want a <c>bool</c> or a result type instead — those callers can wrap this in a
		/// <c>try</c>/<c>catch</c> of their own. Nothing here can force the exception type: this library no
		/// longer ships one for that purpose, so throw whatever fits the caller.
		/// </para>
		/// </remarks>
		void Validate();
	}
}
