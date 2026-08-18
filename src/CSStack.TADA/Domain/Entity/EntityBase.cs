namespace CSStack.TADA
{
	/// <summary>
	/// Base class for entities.
	/// </summary>
	/// <typeparam name="TSelf">Entity type. Pass the type that inherits this class.</typeparam>
	/// <typeparam name="TIdentifier">Entity identifier type</typeparam>
	/// <remarks>
	/// <para>
	/// <b>Entities are compared by identity, never by their contents.</b> Two instances are equal when
	/// they have the same run-time type <i>and</i> equal <see cref="Identifier"/>s; every other property is
	/// ignored, so an entity stays equal to itself after it has been modified or rebuilt from storage.
	/// The run-time type is part of the comparison, so two entities that share a base class — for example
	/// <c>Admin</c> and <c>Guest</c>, both inheriting <c>User : EntityBase&lt;User, UserId&gt;</c> — are
	/// never equal even when their identifiers match. Implement <see cref="IValueObject"/> instead when
	/// value equality is what you want.
	/// </para>
	/// <para>
	/// This class provides no <c>Create</c> / <c>Reconstruct</c> contract, unlike
	/// <see cref="ISingleValueObject{TValue, TSelf}"/>. How an entity is built is left to the entity: the
	/// recommended shape is a private constructor plus a static <c>Create</c> (applies the invariants of a
	/// new entity) and a static <c>Reconstruct</c> (rebuilds an already-persisted entity without
	/// re-applying them), but nothing enforces it. What an entity must have is an identifier, and identity
	/// equality based on it.
	/// </para>
	/// </remarks>
	public abstract class EntityBase<TSelf, TIdentifier> : IEntity<TIdentifier>, IEquatable<TSelf>
		where TSelf : EntityBase<TSelf, TIdentifier>
		where TIdentifier : notnull
	{
		/// <inheritdoc/>
		public abstract TIdentifier Identifier { get; }

		/// <inheritdoc/>
		public abstract void Validate();

		/// <summary>
		/// Identity equality. See <see cref="Equals(TSelf)"/>.
		/// </summary>
		/// <param name="left">Entity to compare</param>
		/// <param name="right">Entity to compare</param>
		/// <returns><see langword="true"/> when both are null, or both have the same run-time type and equal identifiers</returns>
		public static bool operator ==(EntityBase<TSelf, TIdentifier>? left, EntityBase<TSelf, TIdentifier>? right)
		{
			if (left is null)
			{
				return right is null;
			}

			return left.Equals(right);
		}

		/// <summary>
		/// Negation of the <c>==</c> operator. See <see cref="Equals(TSelf)"/>.
		/// </summary>
		/// <param name="left">Entity to compare</param>
		/// <param name="right">Entity to compare</param>
		/// <returns><see langword="true"/> when the two are not equal</returns>
		public static bool operator !=(EntityBase<TSelf, TIdentifier>? left, EntityBase<TSelf, TIdentifier>? right)
		{
			return !(left == right);
		}

		/// <inheritdoc/>
		public override bool Equals(object? obj)
		{
			return obj is TSelf other && Equals(other);
		}

		/// <summary>
		/// Identity equality: same run-time type and equal <see cref="Identifier"/>. Any other state is ignored.
		/// </summary>
		/// <param name="other">Entity to compare</param>
		/// <returns><see langword="true"/> when the two denote the same entity</returns>
		public bool Equals(TSelf? other)
		{
			if (other is null)
			{
				return false;
			}
			if (ReferenceEquals(this, other))
			{
				return true;
			}
			if (GetType() != other.GetType())
			{
				return false;
			}

			return EqualityComparer<TIdentifier>.Default.Equals(Identifier, other.Identifier);
		}

		/// <inheritdoc/>
		public override int GetHashCode()
		{
			return HashCode.Combine(GetType(), Identifier);
		}
	}
}
