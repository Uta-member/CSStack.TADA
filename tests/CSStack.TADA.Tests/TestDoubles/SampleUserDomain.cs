using System.Diagnostics.CodeAnalysis;

namespace CSStack.TADA.Tests
{
    // A minimal but complete domain model built the TADA way, shared across the tests as a worked
    // example of how the pieces fit together:
    //
    //   UserId / UserName (value objects) -> User (entity)
    //     -> InMemoryUserRepository (IRepositoryDeletable)
    //     -> FakeTransactionService / FakeSession (ITransactionService<TSession>)
    //     -> CreateUserCommandService (ICommandService<TReq>, the transaction boundary)
    //
    // Everything here is intentionally the "correct usage": not-found is Optional.Empty, validation
    // lives in Create, Reconstruct skips it, the repository never begins/commits/disposes the session,
    // and the command service is the only layer that touches ITransactionManager.

    /// <summary>
    /// The "who and when" recorded alongside a write. See <c>TOperateInfo</c> on
    /// <see cref="IRepository{TEntity, TEntityIdentifier, TOperateInfo, TSession}"/>.
    /// </summary>
    internal sealed record OperateInfo(string OperatorId, DateTimeOffset OperatedAt);

    // ---- Value objects -----------------------------------------------------------------------------

    /// <summary>
    /// Identifier value object. Also implements the single-parameter <see cref="ISingleValueObject{TValue}"/>
    /// so it can be unwrapped with <c>ExchangeValueObjectToPrimitive</c>.
    /// </summary>
    internal sealed record UserId : ISingleValueObject<Guid, UserId>, ISingleValueObject<Guid>
    {
        private UserId(Guid value)
        {
            Value = value;
        }

        public Guid Value { get; }

        public static UserId Create(Guid value)
        {
            // Validation belongs here and nowhere else.
            if (value == Guid.Empty)
            {
                throw new ValueObjectInvalidException($"{nameof(UserId)} must not be empty.");
            }

            return new UserId(value);
        }

        /// <summary>Restore from a persisted value without re-validating.</summary>
        public static UserId Reconstruct(Guid value)
        {
            return new UserId(value);
        }

        /// <summary>Convenience factory for a fresh identifier.</summary>
        public static UserId New()
        {
            return Create(Guid.NewGuid());
        }
    }

    /// <summary>
    /// A length-bounded string value object. Publishes its bounds through
    /// <see cref="ILengthDefinedSingleValueObject"/> and enforces them in <see cref="Create"/>.
    /// </summary>
    internal sealed record UserName
        : ISingleValueObject<string, UserName>, ISingleValueObject<string>, ILengthDefinedSingleValueObject
    {
        private UserName(string value)
        {
            Value = value;
        }

        public static int MaxLength => 16;

        public static int MinLength => 1;

        public string Value { get; }

        public static UserName Create(string value)
        {
            if (value is null)
            {
                throw new ValueObjectNullException($"{nameof(UserName)} must not be null.");
            }
            if (value.Length < MinLength || value.Length > MaxLength)
            {
                throw new ValueObjectLengthException(
                    minLength: MinLength,
                    maxLength: MaxLength,
                    currentLength: value.Length);
            }

            return new UserName(value);
        }

        public static UserName Reconstruct(string value)
        {
            return new UserName(value);
        }
    }

    // ---- Entity ------------------------------------------------------------------------------------

    /// <summary>
    /// The single entity of the user aggregate. Identity equality comes from <see cref="EntityBase{TSelf, TIdentifier}"/>.
    /// </summary>
    internal sealed class User : EntityBase<User, UserId>
    {
        private User(UserId identifier, UserName name)
        {
            Identifier = identifier;
            Name = name;
        }

        public override UserId Identifier { get; }

        public UserName Name { get; private set; }

        public static User Create(UserId identifier, UserName name)
        {
            return new User(identifier, name);
        }

        public static User Reconstruct(UserId identifier, UserName name)
        {
            return new User(identifier, name);
        }

        public void Rename(UserName name)
        {
            Name = name;
        }
    }

    // ---- Persistence -------------------------------------------------------------------------------

    /// <summary>
    /// One persisted row. Carries the primitive columns plus the operate info of the last write; the
    /// entity is rebuilt from the primitives on read, so operate info stays out of the entity.
    /// </summary>
    internal sealed record UserRow(Guid Id, string Name, OperateInfo OperateInfo);

    /// <summary>
    /// The committed state — stands in for a database table keyed by user id.
    /// </summary>
    internal sealed class InMemoryUserStore
    {
        private readonly Dictionary<Guid, UserRow> _rows = new();

        public int Count => _rows.Count;

        public bool Contains(Guid id)
        {
            return _rows.ContainsKey(id);
        }

        public void Remove(Guid id)
        {
            _rows.Remove(id);
        }

        public void Set(UserRow row)
        {
            _rows[row.Id] = row;
        }

        public bool TryGet(Guid id, [MaybeNullWhen(false)] out UserRow row)
        {
            return _rows.TryGetValue(id, out row);
        }
    }

    /// <summary>
    /// A unit of work over <see cref="InMemoryUserStore"/>. Writes are buffered in an overlay and become
    /// durable only on <see cref="Commit"/>; reads see the overlay first, so a caller reads its own
    /// uncommitted writes within the same transaction.
    /// </summary>
    internal sealed class FakeSession : IDisposable
    {
        // Overlay of pending changes; a null value means a delete is pending for that id.
        private readonly Dictionary<Guid, UserRow?> _pending = new();
        private readonly InMemoryUserStore _store;

        public FakeSession(InMemoryUserStore store)
        {
            _store = store;
        }

        public bool IsCommitted { get; private set; }

        public bool IsDisposed { get; private set; }

        public bool IsRolledBack { get; private set; }

        public void Commit()
        {
            foreach (var (id, row) in _pending)
            {
                if (row is null)
                {
                    _store.Remove(id);
                }
                else
                {
                    _store.Set(row);
                }
            }

            _pending.Clear();
            IsCommitted = true;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }

        public UserRow? Read(Guid id)
        {
            if (_pending.TryGetValue(id, out var pending))
            {
                return pending;
            }

            return _store.TryGet(id, out var row) ? row : null;
        }

        public void Rollback()
        {
            _pending.Clear();
            IsRolledBack = true;
        }

        public void StageDelete(Guid id)
        {
            _pending[id] = null;
        }

        public void StageSave(UserRow row)
        {
            _pending[row.Id] = row;
        }
    }

    /// <summary>
    /// Transaction service for <see cref="FakeSession"/>. The manager owns the session — this service
    /// begins, commits and rolls back, but never disposes it.
    /// </summary>
    internal sealed class FakeTransactionService : ITransactionService<FakeSession>
    {
        private readonly InMemoryUserStore _store;

        public FakeTransactionService(InMemoryUserStore store)
        {
            _store = store;
        }

        /// <summary>The most recently begun session, so tests can assert what happened to it.</summary>
        public FakeSession? LastSession { get; private set; }

        public ValueTask<FakeSession> BeginAsync(CancellationToken cancellationToken = default)
        {
            LastSession = new FakeSession(_store);
            return ValueTask.FromResult(LastSession);
        }

        public ValueTask CommitAsync(FakeSession session, CancellationToken cancellationToken = default)
        {
            session.Commit();
            return ValueTask.CompletedTask;
        }

        public ValueTask RollbackAsync(FakeSession session, CancellationToken cancellationToken = default)
        {
            session.Rollback();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// In-memory repository for <see cref="User"/>. Restores whole entities and upserts them; it takes the
    /// session in rather than owning it, and returns <see cref="Optional{TEntity}.Empty"/> — never null —
    /// when nothing is found.
    /// </summary>
    internal sealed class InMemoryUserRepository : IRepositoryDeletable<User, UserId, OperateInfo, FakeSession>
    {
        public ValueTask DeleteAsync(
            FakeSession session,
            User entity,
            OperateInfo operateInfo,
            CancellationToken cancellationToken = default)
        {
            session.StageDelete(entity.Identifier.Value);
            return ValueTask.CompletedTask;
        }

        public ValueTask<Optional<User>> FindByIdentifierAsync(
            FakeSession session,
            UserId identifier,
            CancellationToken cancellationToken = default)
        {
            var row = session.Read(identifier.Value);
            if (row is null)
            {
                // Not found is a normal outcome: return Empty, never `return null` (that would be Some(null)).
                return ValueTask.FromResult(Optional<User>.Empty);
            }

            // Rebuild the entity from persisted primitives — this is what Reconstruct is for.
            var user = User.Reconstruct(UserId.Reconstruct(row.Id), UserName.Reconstruct(row.Name));
            return ValueTask.FromResult(Optional<User>.Some(user));
        }

        public ValueTask SaveAsync(
            FakeSession session,
            User entity,
            OperateInfo operateInfo,
            CancellationToken cancellationToken = default)
        {
            // Upsert. The write takes effect when the manager commits the session, not here.
            session.StageSave(new UserRow(entity.Identifier.Value, entity.Name.Value, operateInfo));
            return ValueTask.CompletedTask;
        }
    }

    // ---- Use case ----------------------------------------------------------------------------------

    /// <summary>Request for <see cref="CreateUserCommandService"/>.</summary>
    internal sealed record CreateUserReq(Guid UserId, string UserName, OperateInfo OperateInfo) : ICommandServiceDTO;

    /// <summary>
    /// Creates a user. This is the transaction boundary: it opens the transaction through the
    /// <see cref="ITransactionManager"/>, pulls the session out and hands it down to the repository.
    /// Nothing below this layer begins, commits or rolls back a transaction.
    /// </summary>
    internal sealed class CreateUserCommandService : ICommandService<CreateUserReq>
    {
        private readonly InMemoryUserRepository _repository;
        private readonly ITransactionManager _transactionManager;

        public CreateUserCommandService(ITransactionManager transactionManager, InMemoryUserRepository repository)
        {
            _transactionManager = transactionManager;
            _repository = repository;
        }

        public ValueTask ExecuteAsync(CreateUserReq req, CancellationToken cancellationToken = default)
        {
            return _transactionManager.ExecuteTransactionAsync<FakeSession>(
                async (sessions, token) =>
                {
                    var session = sessions.GetSession<FakeSession>();

                    // Turn untrusted input into value objects; validation happens inside Create.
                    var userId = UserId.Create(req.UserId);
                    var userName = UserName.Create(req.UserName);

                    var existing = await _repository.FindByIdentifierAsync(session, userId, token);
                    if (existing.HasValue)
                    {
                        // "Already exists" is the use case's call, not the repository's.
                        throw new ObjectAlreadyExistException(typeof(User), req.UserId);
                    }

                    var user = User.Create(userId, userName);
                    await _repository.SaveAsync(session, user, req.OperateInfo, token);
                },
                cancellationToken: cancellationToken);
        }
    }
}
