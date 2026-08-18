# CSStack.TADA

Transaction-Aware Domain Architecture (TADA) — a C# library for building domain models whose
**transaction boundaries are visible in the type signature** instead of hidden in ambient context.

```
dotnet add package CSStack.TADA
```

`net8.0` / `net10.0`, no dependencies. Everything lives in one flat namespace: `using CSStack.TADA;`

日本語の README は[下部](#csstacktada日本語)に続きます。

---

## What TADA is

An ordinary layered domain architecture — entities, value objects, repositories, use cases — with
**one rule that changes everything: the transaction session is an explicit parameter.**

```csharp
// Is this inside a transaction? Read the signature — that is the whole point.
ValueTask<Optional<User>> FindByIdentifierAsync(
    TSession session, UserId identifier, CancellationToken cancellationToken = default);
```

What travels through the layers is the **type parameter**, not the concrete session type: domain
and use case code never names `AppSession`. Only the infrastructure implementation and the DI
registration do.

### Why pass `TSession` through every layer

There are three ways to express "the scope of a transaction":

| Approach | Example | Can you tell you are in a transaction? |
|---|---|---|
| Ambient context | `TransactionScope`, `AsyncLocal` | **No** — only at run time |
| Repository holds it | repository owns a `DbContext` field | **No** — depends on DI lifetime |
| **Explicit parameter** | **TADA** | **Yes** — it is in the signature |

Ambient context compiles, passes tests, and breaks only under production concurrency. Holding the
session in a repository ties transaction lifetime to DI lifetime, so changing `AddScoped` to
`AddTransient` silently breaks correctness.

TADA takes the explicit route and buys: signatures that cannot lie, a boundary you can grep for
(`ExecuteTransactionAsync`), compile-time separation between different stores, independence from DI
lifetimes, and tests that need no container.

**The cost is verbosity, and it is deliberate.** Transaction scope determines business correctness,
so TADA treats it as something too important to leave implicit. See
[docs/architecture.md](docs/architecture.md).

---

## Quick start

A complete, working flow. The expanded version is in [samples/](samples/) — it builds and runs in CI.

Every snippet below is real code — paste them into one file, add
`using CSStack.TADA;` and `using Microsoft.Extensions.DependencyInjection;`, and it runs.

### 1. The session — anything `IDisposable`

Stands in for a `DbContext` or a `DbConnection` + `DbTransaction`. Writes are buffered until commit.

```csharp
public sealed record UserRow(Guid Id, string Name);

public sealed class UserStore                     // the committed state — a "database"
{
    public Dictionary<Guid, UserRow> Rows { get; } = new();
}

public sealed class AppSession : IDisposable
{
    private readonly Dictionary<Guid, UserRow> _pending = new();
    private readonly UserStore _store;

    public AppSession(UserStore store) => _store = store;

    public void Commit()
    {
        foreach (var (id, row) in _pending)
        {
            _store.Rows[id] = row;
        }

        _pending.Clear();
    }

    public void Dispose() { }                     // only ITransactionManager calls this

    public UserRow? Read(Guid id)
        => _pending.TryGetValue(id, out var pending) ? pending
            : _store.Rows.TryGetValue(id, out var row) ? row : null;

    public void Rollback() => _pending.Clear();

    public void Write(UserRow row) => _pending[row.Id] = row;
}
```

### 2. Value object — validate in `Create`, never in `Reconstruct`

```csharp
public sealed record UserName : ISingleValueObject<string, UserName>
{
    private UserName(string value) => Value = value;   // private: Create/Reconstruct are the only ways in

    // Plain static members — no interface to implement. Publishing them costs nothing extra.
    public static int MaxLength => 16;
    public static int MinLength => 1;

    public string Value { get; }

    public static UserName Create(string value)        // untrusted input: validate here
    {
        CheckInvariants(value);
        return new UserName(value);
    }

    public static UserName Reconstruct(string value) => new(value);   // from storage: no validation

    // IValueObject.Validate() is required, but it is not a substitute for Create — it is a separate
    // path for re-checking invariants later, e.g. after Reconstruct restores data written under older rules.
    public void Validate() => CheckInvariants(Value);

    private static void CheckInvariants(string value)
    {
        if (value is null)
        {
            throw new UserNameInvalidException($"{nameof(UserName)} must not be null.");
        }
        if (value.Length < MinLength || value.Length > MaxLength)
        {
            throw new UserNameLengthException(
                minLength: MinLength, maxLength: MaxLength, currentLength: value.Length);
        }
    }
}
```

`UserNameInvalidException` / `UserNameLengthException` are exceptions this project defines itself —
TADA no longer ships value-object exception types, so the thrown type is the caller's choice.

### 3. Entity — identity equality comes from `EntityBase`

```csharp
public sealed class User : EntityBase<User, Guid>
{
    private User(Guid identifier, UserName name) => (Identifier, Name) = (identifier, name);

    public override Guid Identifier { get; }

    public UserName Name { get; private set; }

    public static User Create(Guid identifier, UserName name) => new(identifier, name);

    public static User Reconstruct(Guid identifier, UserName name) => new(identifier, name);

    public void Rename(UserName name) => Name = name;

    // IEntity<TIdentifier>.Validate() is required; delegating to the value objects is often enough.
    public override void Validate() => Name.Validate();
}
```

Equality is *same runtime type and equal `Identifier`* — other properties are ignored, so a renamed
user is still the same user.

> A strongly-typed identifier (`UserId` as a value object) is recommended over a bare `Guid`;
> [samples/](samples/) shows that shape.

### 4. Repository — the interface keeps `TSession` open; absence is `Optional<T>.Empty`

```csharp
public sealed record OperateInfo(string OperatorId, DateTimeOffset OperatedAt);

// Domain layer. Writing AppSession here would make the domain depend on infrastructure.
public interface IUserRepository<TSession> : IRepository<User, Guid, OperateInfo, TSession>
    where TSession : IDisposable;

// Infrastructure layer. The implementation is what closes the type parameter.
public sealed class UserRepository : IUserRepository<AppSession>
{
    public ValueTask<Optional<User>> FindByIdentifierAsync(
        AppSession session, Guid identifier, CancellationToken cancellationToken = default)
    {
        var row = session.Read(identifier);

        // `return null;` would become Some(null) — HasValue is true and callers get a NullReferenceException
        return ValueTask.FromResult(
            row is null
                ? Optional<User>.Empty
                : Optional<User>.Some(User.Reconstruct(row.Id, UserName.Reconstruct(row.Name))));
    }

    // Upsert. Never throws for "already exists" or "not found" — those are not the repository's call.
    public ValueTask SaveAsync(
        AppSession session, User entity, OperateInfo operateInfo,
        CancellationToken cancellationToken = default)
    {
        session.Write(new UserRow(entity.Identifier, entity.Name.Value));
        return ValueTask.CompletedTask;
    }
}
```

The write becomes durable when `ITransactionManager` commits, not when this method returns.
Listing and searching do **not** belong here — that is `IQueryService`.

> **Never write a concrete session type in a domain or use case declaration.** It compiles, but it
> inverts the dependency direction and costs you most of what TADA and DDD are for. Keep it as a
> type parameter — `TSession` for a repository or aggregate service, `T<Aggregate>Session`
> (e.g. `TUserSession`) once a layer can span aggregates, since different aggregates may live in
> different stores. See [docs/architecture.md](docs/architecture.md).

### 5. Transaction service — one per session type, and **never dispose the session**

```csharp
public sealed class AppTransactionService : ITransactionService<AppSession>
{
    private readonly UserStore _store;

    public AppTransactionService(UserStore store) => _store = store;

    public ValueTask<AppSession> BeginAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new AppSession(_store));

    public ValueTask CommitAsync(AppSession session, CancellationToken cancellationToken = default)
    {
        session.Commit();
        return ValueTask.CompletedTask;   // the manager owns the session and disposes it
    }

    public ValueTask RollbackAsync(AppSession session, CancellationToken cancellationToken = default)
    {
        session.Rollback();
        return ValueTask.CompletedTask;
    }
}
```

### 6. Command service — **this is the transaction boundary**

```csharp
// The interface takes no session type parameter, so callers never name AppSession.
// The request lives inside it: one use case, one request type.
public interface ICreateUserCommandService : ICommandService<ICreateUserCommandService.Req>
{
    sealed record Req(string UserName, OperateInfo OperateInfo) : ICommandServiceDTO;
}

// TUserSession, not TSession: a use case may span aggregates that live in different stores.
public sealed class CreateUserCommandService<TUserSession> : ICreateUserCommandService
    where TUserSession : IDisposable
{
    private readonly ITransactionManager _transactionManager;
    private readonly IUserRepository<TUserSession> _repository;

    public CreateUserCommandService(
        ITransactionManager transactionManager, IUserRepository<TUserSession> repository)
        => (_transactionManager, _repository) = (transactionManager, repository);

    public ValueTask ExecuteAsync(
        ICreateUserCommandService.Req req, CancellationToken cancellationToken = default)
        => _transactionManager.ExecuteTransactionAsync<TUserSession>(
            async (sessions, token) =>
            {
                var session = sessions.GetSession<TUserSession>();

                // Untrusted input becomes a value object here; Create throws when it is invalid
                var user = User.Create(Guid.NewGuid(), UserName.Create(req.UserName));
                await _repository.SaveAsync(session, user, req.OperateInfo, token);
            },
            cancellationToken: cancellationToken);
}
```

Nothing below this layer starts a transaction. Throwing inside the body rolls back; returning commits.

> **Declare an interface per service, and nest its DTOs in it as `Req` and `Res`.** The one above
> carries no session type parameter, so presentation code resolves `ICreateUserCommandService` and
> never writes `<AppSession>` — the type argument appears only in the DI registration below. The same
> applies to aggregate services: derive an `IUserAggregateService<TSession>` from
> `IAggregateService<...>` and implement it with an `AggregateServiceBase<...>` subclass, so a use
> case test can substitute the interface instead of building the concrete service and its repository.
> Domain and query services get an interface too — not to hide a session type, but because deriving
> from `ICommandService` / `IQueryService` (or, for a domain service, declaring `ExecuteAsync` by
> hand — TADA ships no common interface for domain services) already fixes one request and one
> response per service, so that is where those types belong.
>
> **Do not put a general `SaveAsync` on an aggregate service.** Its interface is in practice the
> aggregate root, and a method that accepts any entity is the one callers will reach for, bypassing
> the rule the named operation was holding. Close each "read, change, write" round trip inside one
> named operation (`RenameAsync`) instead. See [docs/best-practices.md](docs/best-practices.md).

### 7. Registration — the step that trips everyone up

```csharp
var services = new ServiceCollection();

services.AddSingleton<UserStore>();

// TransactionManager keeps the in-flight sessions in mutable state and is NOT thread-safe.
// A singleton registration mixes sessions across concurrent requests. It must be scoped.
services.AddScoped<ITransactionManager, TransactionManager>();

// Register one per session type. Without it TransactionManager throws InvalidOperationException.
services.AddScoped<ITransactionService<AppSession>, AppTransactionService>();

// This registration is the only place the session type is decided: it binds the use case's
// TUserSession to the repository's TSession. Presentation layer, and nowhere else.
services.AddScoped<IUserRepository<AppSession>, UserRepository>();
services.AddScoped<ICreateUserCommandService, CreateUserCommandService<AppSession>>();

var provider = services.BuildServiceProvider();
```

### 8. Run it — one scope per use case

```csharp
using (var scope = provider.CreateScope())
{
    var createUser = scope.ServiceProvider.GetRequiredService<ICreateUserCommandService>();
    await createUser.ExecuteAsync(
        new ICreateUserCommandService.Req("alice", new OperateInfo("operator-1", DateTimeOffset.UtcNow)));
}

foreach (var row in provider.GetRequiredService<UserStore>().Rows.Values)
{
    Console.WriteLine(row.Name);   // alice
}
```

ASP.NET Core creates the scope per request, so `CreateScope` is only needed outside it.

> For a richer domain, put the aggregate's rules in an `AggregateServiceBase` subclass and let the
> command service call that instead of the repository. See [samples/](samples/).

---

## Layer structure

```
Presentation    Controller / Minimal API / CLI          — knows nothing about transactions
      ↓
UseCase         ICommandService  ← transaction boundary (ITransactionManager lives here)
                IQueryService                             — reads the store directly
      ↓  session passed as an argument
Domain          Entity / ValueObject / IRepository        — never starts a transaction
                IAggregateService / domain services (project-declared)
      ↑  implemented by
Infrastructure  repository impls, ITransactionService<TSession>, the session type
```

The session type follows the same direction: UseCase and Domain only ever see a type parameter,
Infrastructure defines the concrete type and closes it in the implementation, and Presentation ties
the two together in the DI registration.

---

## Components

All 25 public types. Details are behind the links.

**Entities** — [docs/domain-model.md](docs/domain-model.md)

| Type | One line |
|---|---|
| `IEntity<TIdentifier>` | Entity contract: an `Identifier` and a `Validate()` |
| `EntityBase<TSelf, TIdentifier>` | Base class; equality is same runtime type **and** equal identifier; declares `Validate()` abstract |

**Value objects** — [docs/domain-model.md](docs/domain-model.md)

| Type | One line |
|---|---|
| `IValueObject` | One member, `Validate()`. Implement on a `record`, never a `class` |
| `ISingleValueObject<TValue>` | Just `Value`; for generic constraints |
| `ISingleValueObject<TValue, TSelf>` | Adds the `Create` (validates) / `Reconstruct` (does not) contract |

`Validate()` has no default implementation, so implementing types must supply it. It does not replace
validation in `Create` — it is a separate, on-demand recheck, most useful after `Reconstruct` restores
data written under older rules. A value object that wants to publish length bounds does so as plain
`static int MaxLength` / `MinLength` members, no interface required (`ILengthDefinedSingleValueObject`
was removed in v3.0.0 — it only ever published two numbers).

**Repositories** — [docs/domain-model.md](docs/domain-model.md)

| Type | One line |
|---|---|
| `IRepository<TEntity, TEntityIdentifier, TOperateInfo, TSession>` | `FindByIdentifierAsync` + `SaveAsync` (upsert). **No query methods** |
| `IRepositoryDeletable<...>` | Adds `DeleteAsync`, which takes the entity rather than the identifier |

**Aggregates & domain services** — [docs/domain-model.md](docs/domain-model.md), [docs/use-case.md](docs/use-case.md)

| Type | One line |
|---|---|
| `IAggregateService<TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession>` | The 5 type parameters state "one entity, one repository, one service" |
| `AggregateServiceBase<...>` | Base class; implements the lookup, exposes `Repository` to subclasses |

Domain services have no common TADA interface (`IDomainService<TReq>` / `IDomainService<TReq, TRes>` /
`IDomainServiceDTO` were removed in v3.0.0 — the shape a domain service needs varies too much between
projects for a shared parent to earn its keep). The convention survives: declare an interface, declare
`ExecuteAsync` on it by hand, and nest the request as `Req`. See
[samples/CSStack.TADA.Sample/Domain/UserNameUniquenessService.cs](samples/CSStack.TADA.Sample/Domain/UserNameUniquenessService.cs).

**Use cases** — [docs/use-case.md](docs/use-case.md)

| Type | One line |
|---|---|
| `ICommandService<TReq>` | A state-changing use case. **The transaction boundary** |
| `ICommandService<TReq, TRes>` | Same, with a response. Return a DTO, never an entity |
| `ICommandServiceDTO` | DTO marker. Plain arguments + operate info; no session, no entity |
| `IQueryService<TReq, TRes>` | Read-only use case. Reads the store directly, bypassing repositories |
| `IQueryService<TRes>` | No-argument query. **The single type parameter is the response** |
| `IQueryServiceDTO` | DTO marker. Plain data shaped for the caller |

**Transactions** — [docs/use-case.md](docs/use-case.md), [docs/architecture.md](docs/architecture.md)

| Type | One line |
|---|---|
| `ITransactionManager` | Runs a unit of work across one or more sessions; owns and disposes them |
| `TransactionManager` | Default implementation. **Register as scoped** |
| `TransactionSessions` | The sessions handed to the body; valid only during that call |
| `ITransactionService` | Non-generic base. Do not implement directly |
| `ITransactionService<TSession>` | One per session type: begin / commit / rollback. **Never dispose** |

**Utilities** — [docs/optional.md](docs/optional.md)

| Type | One line |
|---|---|
| `Optional<TValue>` | Three states: `None` / `Some(null)` / `Some(value)` |
| `OptionalExtensions` | `Map` / `Select` / `Bind` / `SelectMany` / `Where` + value-object helpers |

**Exceptions** — [docs/api-reference.md](docs/api-reference.md#例外)

| Type | One line |
|---|---|
| `TADAException` | Base of every exception below |
| `TransactionSessionNotFoundException` | A session was requested before it was begun |
| `NestedTransactionException` | A transaction was started inside another one on the same manager |

These are the only three exceptions the library still ships, and the only two it actually throws
itself (`TADAException` is a base, not something thrown directly). Domain-facing exception types
(missing object, duplicate object, invalid value object, forbidden operation) were removed in v3.0.0:
half-hearted helpers were judged worse than each project defining exceptions in its own domain
vocabulary. See
[samples/CSStack.TADA.Sample/Domain/UserExceptions.cs](samples/CSStack.TADA.Sample/Domain/UserExceptions.cs)
for the pattern (including a case simple enough to just throw `ArgumentException`).

---

## Rules that bite

The full list with wrong/right code is in [docs/best-practices.md](docs/best-practices.md).

1. **Never write `return null;` for `Optional<T>`.** The implicit conversion makes it `Some(null)`,
   whose `HasValue` is true — callers get true out of `TryGetValue` and then a
   `NullReferenceException`. Absence is `Optional<T>.Empty`
2. **Register `TransactionManager` as scoped.** It is not thread-safe and holds the in-flight sessions
3. **Never dispose the session in `ITransactionService`.** The manager owns it on every path
4. **Commits across multiple sessions are not atomic.** Not a two-phase commit coordinator
5. **Only `ICommandService` starts transactions**, and **never nested.** A command service calling
   another command service shares the scoped manager and throws `NestedTransactionException`; extract
   the shared work into a domain service and call it from the same transaction body
6. **Repositories never throw for "not found."** Absence is a normal result, returned as
   `Optional<T>.Empty`; turning it into an exception is the aggregate service's or use case's call
7. **Never add query methods to `IRepository`.** Listing and searching belong to `IQueryService`
8. **Validate in `Create`, not in `Reconstruct`.** `Reconstruct` restores data written under older rules
9. **Never name a concrete session type in the domain or use case layer.** Take it as a type
   parameter — `TSession` for repositories and aggregate services, `T<Aggregate>Session` for layers
   that can span aggregates — and let the infrastructure implementation and the DI registration
   pick the concrete type. Baking `AppSession` into a domain interface compiles and inverts the
   dependency direction
10. **Declare an interface for every service, then implement it.** Injecting an
    `AggregateServiceBase<...>` subclass directly forces every use case test to build that class and
    its repository; resolving a use case through `ICommandService<TReq, TRes>` forces presentation
    code to name `CreateUserCommandService<AppSession>`. Domain and query services get an interface
    too — as the home of their DTOs (11)
11. **Nest the request and response in the interface that uses them, as `Req` and `Res`.** Deriving
    from `ICommandService` and friends already fixes one request type and one response type per
    service, so the DTOs correspond one to one with the interface. Left flat in the namespace they
    cannot be reached from it, and another use case's request still type-checks
12. **Do not put a general `SaveAsync` on an aggregate service.** That interface is in practice the
    aggregate root; a method accepting any entity is the one callers reach for, bypassing the rule
    `RegisterAsync` was holding. Close "read, change, write" inside one named operation
    (`RenameAsync`)

---

## Documentation

| Document | Contents |
|---|---|
| [docs/architecture.md](docs/architecture.md) | **Why `TSession` is passed everywhere**; differences from DDD / Clean Architecture |
| [docs/getting-started.md](docs/getting-started.md) | Zero to running, in 7 steps. DI registration included |
| [docs/best-practices.md](docs/best-practices.md) | Every rule, with wrong/right code |
| [docs/api-reference.md](docs/api-reference.md) | All 25 public types and the meaning of each type parameter |
| [docs/domain-model.md](docs/domain-model.md) | Entities, value objects, repositories, aggregates |
| [docs/use-case.md](docs/use-case.md) | The three service families and where the transaction begins |
| [docs/optional.md](docs/optional.md) | The three states of `Optional<T>` |
| [docs/migration.md](docs/migration.md) | v1 → v2 → v3 upgrade steps |
| [samples/](samples/) | An end-to-end app that builds and runs in CI |

## Changelog

See [CHANGELOG.md](CHANGELOG.md). v3.0.0 contains breaking changes to the transaction APIs —
[docs/migration.md](docs/migration.md) has the upgrade steps.

## License

MIT. See [LICENSE.txt](LICENSE.txt).

---

# CSStack.TADA（日本語）

Transaction-Aware Domain Architecture（TADA）を実装するための C# ライブラリ。
**トランザクションの範囲を暗黙の文脈ではなく型として引数に持たせる**のが唯一にして最大の特徴です。

```
dotnet add package CSStack.TADA
```

`net8.0` / `net10.0` のマルチターゲット。依存パッケージはありません。
namespace はフラットな `CSStack.TADA` の 1 つだけなので、`using CSStack.TADA;` で全部使えます。

## TADA とは

エンティティ・値オブジェクト・リポジトリ・ユースケースからなる普通のレイヤードアーキテクチャに、
**「トランザクションセッションを明示的な引数にする」という 1 つのルール**を加えたものです。

```csharp
// このメソッドはトランザクションの中で動くのか？ → シグネチャを読めば分かる
ValueTask<Optional<User>> FindByIdentifierAsync(
    TSession session, UserId identifier, CancellationToken cancellationToken = default);
```

レイヤーを貫くのは**型引数**であって、セッションの具体型ではありません。
ドメイン層とユースケース層は `AppSession` という名前を知らず、
具体型を名指しするのはインフラ層の実装と DI 登録だけです。

### なぜ `TSession` を全レイヤーに引き回すのか

トランザクションの範囲の表し方は 3 通りあります。

| 方式 | 代表例 | トランザクション中かどうかの判別 |
|---|---|---|
| 暗黙の文脈 | `TransactionScope` / `AsyncLocal` | **できない**（実行時にしか分からない） |
| リポジトリが握る | リポジトリが `DbContext` をフィールドに持つ | **できない**（DI のライフタイム次第） |
| **明示的な引数** | **TADA** | **できる**（シグネチャに出ている） |

暗黙の文脈は、コンパイルが通り、テストも通り、**本番の同時実行時だけ壊れます。**
リポジトリがセッションを握る方式では、トランザクションの寿命が DI スコープの寿命に縛られ、
`AddScoped` を `AddTransient` に変えただけで暗黙に壊れます。

明示的な引数にすると、シグネチャが嘘をつかず、境界を grep でき（`ExecuteTransactionAsync`）、
別のストアを型で取り違えられなくなり、DI のライフタイムに依存せず、
テストに DI コンテナが要らなくなります。

**代償は冗長さで、これは意図的です。** トランザクションの範囲は業務の正しさに直結するので、
暗黙にしてよい種類の詳細ではない、というのがこのアーキテクチャの前提です。
→ [docs/architecture.md](docs/architecture.md)

## 最小の動く例

登録から実行までひととおり。展開版は [samples/](samples/) にあり、CI でビルドと実行まで検証しています。

以下のコードはすべて実際に動きます。1 つのファイルに貼り、
`using CSStack.TADA;` と `using Microsoft.Extensions.DependencyInjection;` を足せば動きます。

### 1. セッション —— `IDisposable` であればよい

`DbContext` や `DbConnection` + `DbTransaction` に相当します。書き込みはコミットまで溜めておきます。

```csharp
public sealed record UserRow(Guid Id, string Name);

public sealed class UserStore                     // コミット済みの状態＝「データベース」
{
    public Dictionary<Guid, UserRow> Rows { get; } = new();
}

public sealed class AppSession : IDisposable
{
    private readonly Dictionary<Guid, UserRow> _pending = new();
    private readonly UserStore _store;

    public AppSession(UserStore store) => _store = store;

    public void Commit()
    {
        foreach (var (id, row) in _pending)
        {
            _store.Rows[id] = row;
        }

        _pending.Clear();
    }

    public void Dispose() { }                     // 呼ぶのは ITransactionManager だけ

    public UserRow? Read(Guid id)
        => _pending.TryGetValue(id, out var pending) ? pending
            : _store.Rows.TryGetValue(id, out var row) ? row : null;

    public void Rollback() => _pending.Clear();

    public void Write(UserRow row) => _pending[row.Id] = row;
}
```

### 2. 値オブジェクト —— 検証は `Create` に書き、`Reconstruct` では検証しない

```csharp
public sealed record UserName : ISingleValueObject<string, UserName>
{
    private UserName(string value) => Value = value;   // private にして入口を 2 つに絞る

    // interface を介さない素の static メンバー。公開するだけで強制はしない
    public static int MaxLength => 16;
    public static int MinLength => 1;

    public string Value { get; }

    public static UserName Create(string value)        // 外部入力用。検証はここだけ
    {
        CheckInvariants(value);
        return new UserName(value);
    }

    public static UserName Reconstruct(string value) => new(value);   // 復元用。検証しない

    // IValueObject.Validate() は必須メンバーだが Create の代わりではない。
    // Reconstruct で古いルールのデータを復元した後などに再チェックするための別経路
    public void Validate() => CheckInvariants(Value);

    private static void CheckInvariants(string value)
    {
        if (value is null)
        {
            throw new UserNameInvalidException($"{nameof(UserName)} に null は指定できません。");
        }
        if (value.Length < MinLength || value.Length > MaxLength)
        {
            // 引数が 3 つとも int。順番を間違えてもコンパイルが通るので名前付きで渡す
            throw new UserNameLengthException(
                minLength: MinLength, maxLength: MaxLength, currentLength: value.Length);
        }
    }
}
```

`Reconstruct` が検証しないのは、ルールを厳しくした後でも古いデータを読み戻せるようにするためです。
`UserNameInvalidException` / `UserNameLengthException` はこのプロジェクトが自分で定義する例外で、
TADA はもう値オブジェクト用の例外クラスを提供しません。

### 3. エンティティ —— 等価性は `EntityBase` が実装済み

```csharp
public sealed class User : EntityBase<User, Guid>
{
    private User(Guid identifier, UserName name) => (Identifier, Name) = (identifier, name);

    public override Guid Identifier { get; }

    public UserName Name { get; private set; }

    public static User Create(Guid identifier, UserName name) => new(identifier, name);

    public static User Reconstruct(Guid identifier, UserName name) => new(identifier, name);

    public void Rename(UserName name) => Name = name;

    // IEntity<TIdentifier>.Validate() は必須メンバー。値オブジェクトへ委譲すればよいことが多い
    public override void Validate() => Name.Validate();
}
```

等価性は「実行時型が同じ、かつ `Identifier` が等しい」。他のプロパティは見ないので、
名前を変えても同じユーザーのままです。

> 識別子も `UserId` のような値オブジェクトにするのが推奨です（→ [samples/](samples/)）。
> ここでは短くするため `Guid` のままにしています。

### 4. リポジトリ —— 不在は `Optional<T>.Empty`。`null` を返さない

```csharp
public sealed record OperateInfo(string OperatorId, DateTimeOffset OperatedAt);

// ドメイン層。ここに AppSession と書くとドメインがインフラに依存してしまう
public interface IUserRepository<TSession> : IRepository<User, Guid, OperateInfo, TSession>
    where TSession : IDisposable;

// インフラ層。型引数を閉じるのは実装の側
public sealed class UserRepository : IUserRepository<AppSession>
{
    public ValueTask<Optional<User>> FindByIdentifierAsync(
        AppSession session, Guid identifier, CancellationToken cancellationToken = default)
    {
        var row = session.Read(identifier);

        // `return null;` は Some(null) になる。HasValue が true のまま NullReferenceException になる
        return ValueTask.FromResult(
            row is null
                ? Optional<User>.Empty
                : Optional<User>.Some(User.Reconstruct(row.Id, UserName.Reconstruct(row.Name))));
    }

    // upsert。「既に居る」も「見つからない」も例外にしない（判断するのは呼び出し側）
    public ValueTask SaveAsync(
        AppSession session, User entity, OperateInfo operateInfo,
        CancellationToken cancellationToken = default)
    {
        session.Write(new UserRow(entity.Identifier, entity.Name.Value));
        return ValueTask.CompletedTask;
    }
}
```

書き込みが確定するのは `ITransactionManager` がコミットしたときで、このメソッドが戻った時点ではありません。
一覧・条件検索のメソッドはここに足しません（`IQueryService` の仕事です）。

> **ドメイン層・ユースケース層の宣言に具体的なセッション型を書かないこと。** コンパイルは通りますが、
> 依存の向きが逆転し、TADA と DDD の利点がほぼ消えます。セッション型は型引数のまま受け取り、
> リポジトリ・集約サービスは `TSession`、集約をまたぎうる層（ドメインサービス・ユースケース）は
> `T[集約名]Session`（`TUserSession` など）と名付けます。集約ごとにストアが違えば
> セッション型も違うためです。→ [docs/architecture.md](docs/architecture.md)

### 5. トランザクションサービス —— セッション型ごとに 1 つ。**Dispose しない**

```csharp
public sealed class AppTransactionService : ITransactionService<AppSession>
{
    private readonly UserStore _store;

    public AppTransactionService(UserStore store) => _store = store;

    public ValueTask<AppSession> BeginAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new AppSession(_store));

    public ValueTask CommitAsync(AppSession session, CancellationToken cancellationToken = default)
    {
        session.Commit();
        return ValueTask.CompletedTask;   // 所有権はマネージャーにある
    }

    public ValueTask RollbackAsync(AppSession session, CancellationToken cancellationToken = default)
    {
        session.Rollback();
        return ValueTask.CompletedTask;
    }
}
```

### 6. コマンドサービス —— **ここがトランザクションの境界**

```csharp
// 口にはセッション型引数を持たせない。呼び出し側は AppSession を書かずに済む
// リクエストは口の中にネストする。1 ユースケース = リクエスト 1 型なので対応が固定される
public interface ICreateUserCommandService : ICommandService<ICreateUserCommandService.Req>
{
    sealed record Req(string UserName, OperateInfo OperateInfo) : ICommandServiceDTO;
}

// TSession ではなく TUserSession。ユースケースは複数の集約を跨ぎ、ストアが違えば型も違う
public sealed class CreateUserCommandService<TUserSession> : ICreateUserCommandService
    where TUserSession : IDisposable
{
    private readonly ITransactionManager _transactionManager;
    private readonly IUserRepository<TUserSession> _repository;

    public CreateUserCommandService(
        ITransactionManager transactionManager, IUserRepository<TUserSession> repository)
        => (_transactionManager, _repository) = (transactionManager, repository);

    public ValueTask ExecuteAsync(
        ICreateUserCommandService.Req req, CancellationToken cancellationToken = default)
        => _transactionManager.ExecuteTransactionAsync<TUserSession>(
            async (sessions, token) =>
            {
                var session = sessions.GetSession<TUserSession>();

                // 外部入力はここで値オブジェクトに変換する。不正なら Create が例外を投げる
                var user = User.Create(Guid.NewGuid(), UserName.Create(req.UserName));
                await _repository.SaveAsync(session, user, req.OperateInfo, token);
            },
            cancellationToken: cancellationToken);
}
```

これより下の層はトランザクションを開始しません。
本体が例外を投げればロールバックされ、最後まで通ればコミットされます。

> **サービスごとにインターフェースを立て、DTO はその中に `Req` / `Res` としてネストします。** 上の
> `ICreateUserCommandService` はセッション型引数を持たないので、プレゼンテーション層は
> これを解決するだけで `<AppSession>` を書かずに済みます（型引数が現れるのは下の DI 登録だけ）。
> 集約サービスも同じで、`IAggregateService<...>` を継承した `IUserAggregateService<TSession>` を
> 宣言し、`AggregateServiceBase<...>` の派生クラスで実装します。こうしておけば、
> ユースケースのテストで集約サービスの具象クラスとリポジトリ実装を組み立てずに済みます。
> ドメインサービスとクエリサービスにも口を立てます。理由はセッション型を隠すためではなく、
> `ICommandService` / `IQueryService` を継承した時点で（ドメインサービスは `ExecuteAsync` を
> 自分で宣言した時点で。TADA に共通のインターフェースは無い）
> 「リクエスト 1 型・レスポンス 1 型」が確定するので、**そこが DTO の置き場所になる**ためです。
>
> **集約サービスに汎用の `SaveAsync` を置かないでください。** その口は実質的に集約ルートで、
> 何でも受け取るメソッドがあれば呼ぶ側はそちらを選び、名前の付いた操作が持っていたルールが
> 素通りされます。「取得 → 変更 → 保存」は `RenameAsync` のような 1 つの操作に閉じてください
> （→ [docs/best-practices.md](docs/best-practices.md)）。

### 7. DI 登録 —— 最初につまずくところ

```csharp
var services = new ServiceCollection();

services.AddSingleton<UserStore>();

// TransactionManager は実行中のセッションを保持し、スレッドセーフではありません。
// Singleton にすると全リクエストでセッションが混線します。必ず Scoped で登録してください。
services.AddScoped<ITransactionManager, TransactionManager>();

// セッション型ごとに登録します。忘れると TransactionManager が InvalidOperationException を投げます。
services.AddScoped<ITransactionService<AppSession>, AppTransactionService>();

// セッション型が確定するのはこの登録だけです。ユースケースの TUserSession と
// リポジトリの TSession を結びつけているのがこの 2 行で、これはプレゼンテーション層の仕事です。
services.AddScoped<IUserRepository<AppSession>, UserRepository>();
services.AddScoped<ICreateUserCommandService, CreateUserCommandService<AppSession>>();

var provider = services.BuildServiceProvider();
```

### 8. 実行 —— 1 ユースケース = 1 スコープ

```csharp
using (var scope = provider.CreateScope())
{
    var createUser = scope.ServiceProvider.GetRequiredService<ICreateUserCommandService>();
    await createUser.ExecuteAsync(
        new ICreateUserCommandService.Req("alice", new OperateInfo("operator-1", DateTimeOffset.UtcNow)));
}

foreach (var row in provider.GetRequiredService<UserStore>().Rows.Values)
{
    Console.WriteLine(row.Name);   // alice
}
```

ASP.NET Core ではリクエストごとにスコープが作られるので、`CreateScope` は不要です。

> 集約のルールが増えてきたら `AggregateServiceBase` を継承したサービスに置き、
> コマンドサービスからはそちらを呼びます。→ [samples/](samples/)

## レイヤー構成

```
Presentation    Controller / Minimal API / CLI          — トランザクションを知らない
      ↓
UseCase         ICommandService  ← トランザクションの境界（ITransactionManager はここ）
                IQueryService                             — ストアを直接読む
      ↓  セッションを引数で渡す
Domain          Entity / ValueObject / IRepository        — トランザクションを開始しない
                IAggregateService / ドメインサービス（プロジェクト側で宣言）
      ↑  実装する
Infrastructure  リポジトリ実装 / ITransactionService<TSession> / セッション型
```

セッション型もこの向きに従います。UseCase と Domain が見るのは型引数だけで、
Infrastructure が具体型を定義して実装で閉じ、Presentation が DI 登録で両者を結びつけます。

## 主要コンポーネント

公開型は 25 個。詳細は各リンク先にあります。

**エンティティ** — [docs/domain-model.md](docs/domain-model.md)

| 型 | 概要 |
|---|---|
| `IEntity<TIdentifier>` | エンティティの契約。要求するのは `Identifier` と `Validate()` |
| `EntityBase<TSelf, TIdentifier>` | 基底クラス。等価性は「実行時型が同じ**かつ**識別子が等しい」。`Validate()` は抽象宣言 |

**値オブジェクト** — [docs/domain-model.md](docs/domain-model.md)

| 型 | 概要 |
|---|---|
| `IValueObject` | メンバーは `Validate()` のみ。`class` ではなく `record` で実装する |
| `ISingleValueObject<TValue>` | `Value` のみ。ジェネリック制約用 |
| `ISingleValueObject<TValue, TSelf>` | `Create`（検証あり）/ `Reconstruct`（検証なし）の規約が付く |

`Validate()` は既定実装が無い必須メンバー。`Create` の検証を置き換えるものではなく、
`Reconstruct` で古いルールのデータを復元した後などに再チェックするための別経路。
長さの上下限を公開したい値オブジェクトは、interface を介さず素の `static int MaxLength` /
`MinLength` を宣言するだけでよい（`ILengthDefinedSingleValueObject` は v3.0.0 で削除。
公開する数値が 2 つだけの薄いマーカーだった）。

**リポジトリ** — [docs/domain-model.md](docs/domain-model.md)

| 型 | 概要 |
|---|---|
| `IRepository<TEntity, TEntityIdentifier, TOperateInfo, TSession>` | `FindByIdentifierAsync` と `SaveAsync`（upsert）。**検索系は無い** |
| `IRepositoryDeletable<...>` | `DeleteAsync` が加わる。識別子ではなくエンティティを受け取る |

**集約・ドメインサービス** — [docs/domain-model.md](docs/domain-model.md) / [docs/use-case.md](docs/use-case.md)

| 型 | 概要 |
|---|---|
| `IAggregateService<TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession>` | 型引数 5 個が「エンティティ 1・リポジトリ 1・サービス 1」を表明する |
| `AggregateServiceBase<...>` | 基底クラス。取得を実装し、`Repository` を派生クラスに公開する |

ドメインサービスに TADA 由来の共通インターフェースは無い（`IDomainService<TReq>` /
`IDomainService<TReq, TRes>` / `IDomainServiceDTO` は v3.0.0 で削除。扱う対象・引数・戻り値が
プロジェクトごとに柔軟すぎて、共通の親を立てても効果が薄かったため）。
「専用の口を立て、リクエストを `Req` としてネストする」という規約は変わらず、
`ExecuteAsync` を自分で 1 つ宣言するだけになる。
→ [samples/CSStack.TADA.Sample/Domain/UserNameUniquenessService.cs](samples/CSStack.TADA.Sample/Domain/UserNameUniquenessService.cs)

**ユースケース** — [docs/use-case.md](docs/use-case.md)

| 型 | 概要 |
|---|---|
| `ICommandService<TReq>` | 状態を変えるユースケース。**トランザクションの境界** |
| `ICommandService<TReq, TRes>` | 戻り値がある版。エンティティではなく DTO を返す |
| `ICommandServiceDTO` | DTO マーカー。素の引数と操作情報のみ。セッションもエンティティも持たない |
| `IQueryService<TReq, TRes>` | 読み取り専用。リポジトリを通さずストアを直接読む |
| `IQueryService<TRes>` | 引数の無いクエリ。**型引数 1 個はレスポンス**（コマンドと逆） |
| `IQueryServiceDTO` | DTO マーカー。呼び出し側に合わせた素のデータ |

**トランザクション** — [docs/use-case.md](docs/use-case.md) / [docs/architecture.md](docs/architecture.md)

| 型 | 概要 |
|---|---|
| `ITransactionManager` | 1 つ以上のセッションをまたいで実行する。セッションを所有し Dispose する |
| `TransactionManager` | 既定の実装。**Scoped で登録する** |
| `TransactionSessions` | 本体に渡されるセッション集合。その呼び出しの中でのみ有効 |
| `ITransactionService` | 非ジェネリックの基底。直接実装しない |
| `ITransactionService<TSession>` | セッション型ごとに 1 つ。begin / commit / rollback。**Dispose しない** |

**ユーティリティ** — [docs/optional.md](docs/optional.md)

| 型 | 概要 |
|---|---|
| `Optional<TValue>` | `None` / `Some(null)` / `Some(value)` の三状態 |
| `OptionalExtensions` | `Map` / `Select` / `Bind` / `SelectMany` / `Where` と値オブジェクト用ヘルパー |

**例外** — [docs/api-reference.md](docs/api-reference.md#例外)

| 型 | 概要 |
|---|---|
| `TADAException` | すべての基底 |
| `TransactionSessionNotFoundException` | 開始されていないセッションを要求した |
| `NestedTransactionException` | 実行中のトランザクションの中で、同じマネージャーのトランザクションを開始した |

TADA が今も提供する例外はこの 3 個だけで、実際に投げるのは 2 個（`TADAException` は基底）。
ドメイン向けの例外（対象が見つからない・既に存在する・値オブジェクトの不変条件違反・
許されない操作）は v3.0.0 で削除された。中途半端なヘルパーより、各プロジェクトが自分の
ドメインの語彙で例外を定義したほうが健全と判断したため。
→ [samples/CSStack.TADA.Sample/Domain/UserExceptions.cs](samples/CSStack.TADA.Sample/Domain/UserExceptions.cs)
（単純な不変条件なら `ArgumentException` で足りる例もある）

## 必ず踏む地雷

正しい書き方と対にした一覧は [docs/best-practices.md](docs/best-practices.md) にあります。

1. **`Optional<T>` で `return null;` と書かない。** 暗黙変換によって `Some(null)` になり、
   `HasValue` が true のまま `TryGetValue` が true を返して `NullReferenceException` になります。
   不在は `Optional<T>.Empty`
2. **`TransactionManager` は Scoped で登録する。** スレッドセーフではなく、実行中のセッションを保持します
3. **`ITransactionService` の実装側でセッションを `Dispose` しない。** 所有権はマネージャーにあります
4. **複数セッションの commit はアトミックではない。** 2 相コミットではありません
5. **トランザクションを開始してよいのは `ICommandService` だけ。入れ子にもできません。**
   コマンドサービスが別のコマンドサービスを呼ぶと Scoped の同一マネージャーに行き着き、
   `NestedTransactionException` になります。共通処理はドメインサービスに切り出し、
   同じトランザクションの本体から呼んでください
6. **リポジトリは「見つからない」を例外にしない。** 不在は `Optional<T>.Empty` で返る正常な結果で、
   例外にするかどうかは集約サービス / ユースケースが決めます
7. **`IRepository` に検索系メソッドを足さない。** 一覧・条件検索は `IQueryService` の仕事
8. **検証は `Create` に書き、`Reconstruct` では検証しない**
9. **ドメイン層・ユースケース層に具体的なセッション型を書かない。** 型引数として受け取り
   （リポジトリ・集約サービスは `TSession`、集約をまたぎうる層は `T[集約名]Session`）、
   具体型はインフラ層の実装と DI 登録で決めます。ドメインのインターフェースに `AppSession` と
   書くとコンパイルは通りますが、依存の向きが逆転します
10. **サービスはインターフェースを立ててから実装する。**
    `AggregateServiceBase<...>` の派生クラスを直接注入すると、ユースケースのテストが
    その具象クラスとリポジトリ実装を組み立てる話になります。ユースケースを
    `ICommandService<TReq, TRes>` で解決すると、プレゼンテーション層が
    `CreateUserCommandService<AppSession>` を名指しすることになります。
    ドメインサービスとクエリサービスにも、DTO の置き場所として口を立てます（11 番）
11. **リクエスト / レスポンスは、それを使う口の中に `Req` / `Res` としてネストする。**
    `ICommandService` などを継承した時点で「リクエスト 1 型・レスポンス 1 型」が確定するので、
    DTO は口と 1 対 1 に対応します。外に平らに置くと口から辿れず、
    別のユースケースの DTO を渡しても型が合えば通ってしまいます
12. **集約サービスの口に `SaveAsync` のような汎用的な操作を置かない。**
    その口は実質的に集約ルートです。何でも受け取るメソッドがあれば呼ぶ側はそちらを選び、
    `RegisterAsync` が持っていた「既に居たら失敗」が素通りされます。
    「取得 → 変更 → 保存」は `RenameAsync` のような 1 つの操作に閉じてください

## ドキュメント

| ドキュメント | 内容 |
|---|---|
| [docs/architecture.md](docs/architecture.md) | **なぜ `TSession` を引き回すのか**。DDD / クリーンアーキテクチャとの差分 |
| [docs/getting-started.md](docs/getting-started.md) | ゼロから動かすまでの 7 ステップ。DI 登録を含む |
| [docs/best-practices.md](docs/best-practices.md) | 規約の一覧。間違い → 正しい形 → なぜ |
| [docs/api-reference.md](docs/api-reference.md) | 公開型 25 個と型引数の意味 |
| [docs/domain-model.md](docs/domain-model.md) | エンティティ / 値オブジェクト / リポジトリ / 集約 |
| [docs/use-case.md](docs/use-case.md) | 3 種のサービスの使い分けとトランザクションの境界 |
| [docs/optional.md](docs/optional.md) | `Optional<T>` の三状態 |
| [docs/migration.md](docs/migration.md) | v1 → v2 → v3 の移行手順 |
| [samples/](samples/) | ビルドも実行も CI で検証しているエンドツーエンドのサンプル |

## 変更履歴

[CHANGELOG.md](CHANGELOG.md) を参照してください。
v3.0.0 にはトランザクション周りの破壊的変更が含まれます。
移行手順は [docs/migration.md](docs/migration.md) にあります。

## ライセンス

MIT。[LICENSE.txt](LICENSE.txt) を参照してください。
