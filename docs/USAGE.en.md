# CSStack.TADA Usage Guide (English)

This guide explains how to use each class and interface in CSStack.TADA systematically, with practical examples. The library targets .NET 8 and C# 12.

Contents
- Core concepts and layering
- Entities (use Value Objects, private constructors, Create/Reconstruct, immutable updates)
- Value Objects (ValidateHelper/KeyedValidateHelper)
- Repositories
- Aggregate Services (aggregate root, open on TSession only)
- Domain Services (span multiple aggregates, take TSession from outside, open on TSession only)
- Use Case Services (transaction orchestration with ITransactionManager)
- Transaction Management (database transaction API)
- Utilities (Optional, ValidateHelper)
- Exceptions
- End-to-end example
- Obsolete APIs

Core concepts and layering
- Domain layer: Entities, Value Objects, Domain Services, Repositories, Aggregate Services.
- Use Case layer: Command/Query services that orchestrate domain operations and transactions.
- Infrastructure layer (your app): Implement IRepository, ITransactionService, etc.
- Design note: Only TSession should remain open and be closed by DI at registration time. Repository (IRepository<...>) and operate info type (e.g., string) are fixed at the AggregateService boundary.

Entities
- Requirements in this toolkit
  - Properties should use Value Objects (not raw primitives) where appropriate.
  - Constructor must be private; expose static Create (with validation) and Reconstruct (without validation) methods.
  - Updating any value should return a new instance (immutability).
- IEntity<TIdentifier>
  - Contract for an entity with a strongly-typed identifier.
  - Members: Identifier (get), Validate(), IsInvalidValue (bool)
- EntityBase<TSelf, TIdentifier>
  - Base class implementing equality by Identifier and IsInvalidValue via Validate().

Example (entity using value objects and factory methods)
```csharp
public sealed record UserId(Guid Value);

public sealed record UserName : ValueObjectBase,
    ISingleValueObject<string, UserName>, ILengthDefinedSingleValueObject
{
    public string Value { get; }
    public static int MinLength => 1;
    public static int MaxLength => 50;

    private UserName(string value) => Value = value;

    public static UserName Create(string value)
    {
        var vo = new UserName(value);
        vo.Validate();
        return vo;
    }

    public static UserName Reconstruct(string value) => new(value);

    public override void Validate()
    {
        // Single-value VO: use ValidateHelper
        var v = new ValidateHelper();
        v.AddNullCheck(Value, "Name is required.");
        v.AddStrLengthCheck(Value, MinLength, MaxLength, "Name length out of range.");
        v.ExecuteValidateWithThrowException();
    }
}

public sealed class User : EntityBase<User, UserId>
{
    public override UserId Identifier { get; }
    public UserName Name { get; }

    private User(UserId id, UserName name)
    {
        Identifier = id;
        Name = name;
    }

    // Create: with validation
    public static User Create(UserId id, UserName name)
    {
        var entity = new User(id, name);
        entity.Validate();
        return entity;
    }

    // Reconstruct: without validation (for repository rehydration)
    public static User Reconstruct(UserId id, UserName name) => new(id, name);

    // Immutable update example
    public User WithName(UserName newName)
    {
        var updated = new User(Identifier, newName);
        updated.Validate();
        return updated;
    }

    public override void Validate()
    {
        // Entity (or multi-value VO): use KeyedValidateHelper
        var v = new KeyedValidateHelper<string>();
        v.Add("Id", () => { if (Identifier.Value == Guid.Empty) throw new ValueObjectInvalidException("Id empty"); });
        v.Add("Name", Name.Validate);
        v.ExecuteValidateWithThrowException();
    }
}
```

Value Objects
- IValueObject / ValueObjectBase
  - For immutable, self-validating objects.
  - Implement Validate(); IsInvalidValue is provided by base.
- ISingleValueObject<TValue, TSelf>
  - Pattern for single-value VO with static Create/Reconstruct. In Validate(), prefer ValidateHelper for single-value checks.
- ILengthDefinedSingleValueObject
  - For VOs with min/max length constraints (static abstract MinLength/MaxLength).
- When a VO has multiple fields, prefer KeyedValidateHelper in Validate() to aggregate reasons with keys.

Repositories
- IRepository<TEntity, TEntityIdentifier, TOperateInfo, TSession>
  - FindByIdentifierAsync: returns Optional<TEntity>
  - SaveAsync: persist changes with operate info and transaction session.
- Repositories should not be accessed directly by DomainService or Use Case; go through AggregateService.

Aggregate Services (aggregate root)
- Purpose: Encapsulate entity and repository; all operations that are complete within the aggregate belong here (e.g., existence check, creation rules, updates).
- Open generics policy: Define aggregate services open on TSession only. Repository and operate info types are fixed at this layer.
- Use AggregateServiceBase as convenience for common queries.

Example aggregate service (open on TSession only)
```csharp
public sealed class UserAggregateService<TSession> :
    AggregateServiceBase<User, UserId, IRepository<User, UserId, string, TSession>, string, TSession>
    where TSession : IDisposable
{
    public UserAggregateService(IRepository<User, UserId, string, TSession> repo) : base(repo) { }

    public async ValueTask<User> CreateAsync(TSession session, UserId id, UserName name, string op, CancellationToken ct)
    {
        var exists = await Repository.FindByIdentifierAsync(session, id, ct);
        if (exists.HasValue) throw new ObjectAlreadyExistException();
        var user = User.Create(id, name);
        await Repository.SaveAsync(session, user, op, ct);
        return user;
    }

    public async ValueTask<User> UpdateNameAsync(TSession session, UserId id, UserName newName, string op, CancellationToken ct)
    {
        var found = await Repository.FindByIdentifierAsync(session, id, ct);
        var user = found.GetValue(defaultValue: null!) ?? throw new ObjectNotFoundException();
        var updated = user.WithName(newName);
        await Repository.SaveAsync(session, updated, op, ct);
        return updated;
    }
}
```

Domain Services
- Domain services may span multiple aggregates. They must receive TSession from outside (e.g., via request DTO) to support both eventual consistency and consistent transactions.
- Do not access repositories directly. Use aggregate services only.
- Open generics policy: Domain services are open on TSession only; they depend on aggregate services that fix repository/operate info types.

Example domain service (open on TSession only)
```csharp
public sealed record RegisterAndGreetReq<TSession>(TSession Session, UserId Id, UserName Name, string Op)
    : IDomainServiceDTO
    where TSession : IDisposable;

public sealed record RegisterAndGreetRes(string Message) : IDomainServiceDTO;

public sealed class RegisterAndGreetService<TSession>(
    UserAggregateService<TSession> users,
    GreeterAggregateService<TSession> greeter)
    : IDomainService<RegisterAndGreetReq<TSession>, RegisterAndGreetRes>
    where TSession : IDisposable
{
    private readonly UserAggregateService<TSession> _users = users;
    private readonly GreeterAggregateService<TSession> _greeter = greeter;

    public async ValueTask<RegisterAndGreetRes> ExecuteAsync(
        RegisterAndGreetReq<TSession> req,
        CancellationToken ct = default)
    {
        var user = await _users.CreateAsync(req.Session, req.Id, req.Name, req.Op, ct);
        var msg = await _greeter.ComposeGreetingAsync(req.Session, user, req.Op, ct);
        return new RegisterAndGreetRes(msg);
    }
}
```

Use Case Services
- Use ITransactionManager to control transactions. A use case can only access the domain through domain services.
- Close TSession by DI.

Example use case orchestrating a transaction (TSession closed by DI)
```csharp
public sealed record RegisterUserCommand(UserId Id, string Name) : ICommandServiceDTO;
public sealed record RegisterUserResult(Guid Id) : ICommandServiceDTO;

public sealed class RegisterUserCommandService<TSession> :
    ICommandService<RegisterUserCommand, RegisterUserResult>
    where TSession : IDisposable
{
    private readonly ITransactionManager _tx;
    private readonly RegisterAndGreetService<TSession> _domain;

    public RegisterUserCommandService(ITransactionManager tx, RegisterAndGreetService<TSession> domain)
    { _tx = tx; _domain = domain; }

    public async ValueTask<RegisterUserResult> ExecuteAsync(RegisterUserCommand req, CancellationToken ct = default)
    {
        await _tx.ExecuteTransactionAsync(
            sessionTypes: [ typeof(TSession) ],
            transactionFunction: async sessions =>
            {
                var session = sessions.GetSession<TSession>();
                var _ = await _domain.ExecuteAsync(new RegisterAndGreetReq<TSession>(session, req.Id, UserName.Create(req.Name), "register"), ct);
            });

        return new RegisterUserResult(req.Id.Value);
    }
}
```

Transaction Management
- Implement ITransactionService<TSession> using the database transaction API (e.g., DbContext.Database.BeginTransaction/Rollback/Commit when using EF Core), not SaveChanges as transaction control.

Example EF Core transaction service
```csharp
public sealed class EfSession : IDisposable
{
    public required MyDbContext Db { get; init; }
    public required IDbContextTransaction Tx { get; init; }
    public void Dispose() { Tx.Dispose(); Db.Dispose(); }
}

public sealed class EfTransactionService : ITransactionService<EfSession>
{
    private readonly IDbContextFactory<MyDbContext> _factory;
    public EfTransactionService(IDbContextFactory<MyDbContext> factory) => _factory = factory;

    public async ValueTask<EfSession> BeginAsync()
    {
        var db = await _factory.CreateDbContextAsync();
        var tx = await db.Database.BeginTransactionAsync();
        return new EfSession { Db = db, Tx = tx };
    }

    public async ValueTask CommitAsync(EfSession s)
    {
        await s.Db.SaveChangesAsync();
        await s.Tx.CommitAsync();
    }

    public async ValueTask RollbackAsync(EfSession s)
    {
        await s.Tx.RollbackAsync();
    }
}
```

Utilities
- Optional<T>
  - Three-state container: None/Some(null)/Some(value).
  - HasValue, Value, TryGetValue(out), Empty, implicit from T.
- ValidateHelper / KeyedValidateHelper<TKey>
  - Single-value VOs: prefer ValidateHelper.
  - Entities and multi-value VOs: prefer KeyedValidateHelper to aggregate validation reasons by key.

Exceptions
- Base: TADAException
- Domain: DomainInvalidOperationException
- Value Object: ValueObjectInvalidException, ValueObjectNullException, ValueObjectLengthException
- Object lifecycle: ObjectNotFoundException, ObjectAlreadyExistException
- Aggregating: MultiReasonException, KeyedMultiReasonException<TKey>

End-to-end example (composition in DI, closing TSession)
```csharp
// Close TSession here
services.AddScoped<ITransactionService<EfSession>, EfTransactionService>();
services.AddScoped<IRepository<User, UserId, string, EfSession>, EfUserRepository>();
services.AddScoped<UserAggregateService<EfSession>>();
services.AddScoped<GreeterAggregateService<EfSession>>();
services.AddScoped<RegisterAndGreetService<EfSession>>();
services.AddScoped<ICommandService<RegisterUserCommand, RegisterUserResult>, RegisterUserCommandService<EfSession>>();
```

Obsolete APIs
- IDomainServiceWithRes<TReq, TRes>, ICommandServiceWithRes<TReq, TRes>, IQueryServiceWithoutReq<TRes>
  - These are obsolete; use the non-obsolete generic interfaces instead as mentioned above.
