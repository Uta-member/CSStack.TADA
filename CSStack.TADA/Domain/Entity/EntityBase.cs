namespace CSStack.TADA
{
    /// <summary>
    /// Base class for entities.
    /// </summary>
    /// <typeparam name="TSelf">Entity type</typeparam>
    /// <typeparam name="TIdentifier">Entity identifier type</typeparam>
    public abstract class EntityBase<TSelf, TIdentifier> : IEntity<TIdentifier>, IEquatable<TSelf>
        where TSelf : EntityBase<TSelf, TIdentifier>
        where TIdentifier : notnull
    {
        /// <inheritdoc/>
        public static bool operator !=(EntityBase<TSelf, TIdentifier>? left, EntityBase<TSelf, TIdentifier>? right)
        {
            return !(left == right);
        }

        /// <inheritdoc/>
        public static bool operator ==(EntityBase<TSelf, TIdentifier>? left, EntityBase<TSelf, TIdentifier>? right)
        {
            if(ReferenceEquals(left, null))
            {
                return ReferenceEquals(right, null);
            }
            return left.Equals(right);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if(obj is null)
            {
                return false;
            }
            if(!(obj is TSelf other))
            {
                return false;
            }
            return Equals(other);
        }

        /// <summary>
        /// Equality comparison. Basically evaluate only by identifier such as ID.
        /// </summary>
        /// <param name="other">Entity to compare</param>
        /// <returns></returns>
        public bool Equals(TSelf? other)
        {
            return other?.Identifier.Equals(Identifier) ?? false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Identifier.GetHashCode();
        }

        /// <inheritdoc/>
        public abstract void Validate();

        /// <inheritdoc/>
        public abstract TIdentifier Identifier { get; }

        /// <inheritdoc/>
        public bool IsInvalidValue
        {
            get
            {
                try
                {
                    Validate();
                    return false;
                }
                catch
                {
                    return true;
                }
            }
        }
    }
}
