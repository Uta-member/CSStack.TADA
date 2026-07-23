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
    AppSession session, UserId identifier, CancellationToken cancellationToken = default);
```

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
public sealed record UserName : ISingleValueObject<string, UserName>, ILengthDefinedSingleValueObject
{
    private UserName(string value) => Value = value;   // private: Create/Reconstruct are the only ways in

    public static int MaxLength => 16;
    public static int MinLength => 1;

    public string Value { get; }

    public static UserName Create(string value)        // untrusted input: validate here
    {
        if (value is null)
        {
            throw new ValueObjectNullException($"{nameof(UserName)} must not be null.");
        }
        if (value.Length < MinLength || value.Length > MaxLength)
        {
            throw new ValueObjectLengthException(
                minLength: MinLength, maxLength: MaxLength, currentLength: value.Length);
        }

        return new UserName(value);
    }

    public static UserName Reconstruct(string value) => new(value);   // from storage: no validation
}
```

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
}
```

Equality is *same runtime type and equal `Identifier`* — other properties are ignored, so a renamed
user is still the same user.

> A strongly-typed identifier (`UserId` as a value object) is recommended over a bare `Guid`;
> [samples/](samples/) shows that shape.

### 4. Repository — absence is `Optional<T>.Empty`, never `null`

```csharp
public sealed record OperateInfo(string OperatorId, DateTimeOffset OperatedAt);

public interface IUserRepository : IRepository<User, Guid, OperateInfo, AppSession>;

public sealed class UserRepository : IUserRepository
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

    // Upsert. Never throws ObjectAlreadyExistException / ObjectNotFoundException.
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
public sealed record CreateUserReq(string UserName, OperateInfo OperateInfo) : ICommandServiceDTO;

public sealed class CreateUserCommandService : ICommandService<CreateUserReq>
{
    private readonly ITransactionManager _transactionManager;
    private readonly IUserRepository _repository;

    public CreateUserCommandService(ITransactionManager transactionManager, IUserRepository repository)
        => (_transactionManager, _repository) = (transactionManager, repository);

    public ValueTask ExecuteAsync(CreateUserReq req, CancellationToken cancellationToken = default)
        => _transactionManager.ExecuteTransactionAsync<AppSession>(
            async (sessions, token) =>
            {
                var session = sessions.GetSession<AppSession>();

                // Untrusted input becomes a value object here; Create throws when it is invalid
                var user = User.Create(Guid.NewGuid(), UserName.Create(req.UserName));
                await _repository.SaveAsync(session, user, req.OperateInfo, token);
            },
            cancellationToken: cancellationToken);
}
```

Nothing below this layer starts a transaction. Throwing inside the body rolls back; returning commits.

### 7. Registration — the step that trips everyone up

```csharp
var services = new ServiceCollection();

services.AddSingleton<UserStore>();

// TransactionManager keeps the in-flight sessions in mutable state and is NOT thread-safe.
// A singleton registration mixes sessions across concurrent requests. It must be scoped.
services.AddScoped<ITransactionManager, TransactionManager>();

// Register one per session type. Without it TransactionManager throws InvalidOperationException.
services.AddScoped<ITransactionService<AppSession>, AppTransactionService>();

services.AddScoped<IUserRepository, UserRepository>();
services.AddScoped<ICommandService<CreateUserReq>, CreateUserCommandService>();

var provider = services.BuildServiceProvider();
```

### 8. Run it — one scope per use case

```csharp
using (var scope = provider.CreateScope())
{
    var createUser = scope.ServiceProvider.GetRequiredService<ICommandService<CreateUserReq>>();
    await createUser.ExecuteAsync(
        new CreateUserReq("alice", new OperateInfo("operator-1", DateTimeOffset.UtcNow)));
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
                IAggregateService / IDomainService
      ↑  implemented by
Infrastructure  repository impls, ITransactionService<TSession>, the session type
```

---

## Components

All 34 public types. Details are behind the links.

**Entities** — [docs/domain-model.md](docs/domain-model.md)

| Type | One line |
|---|---|
| `IEntity<TIdentifier>` | Entity contract: an `Identifier`, and nothing else |
| `EntityBase<TSelf, TIdentifier>` | Base class; equality is same runtime type **and** equal identifier |

**Value objects** — [docs/domain-model.md](docs/domain-model.md)

| Type | One line |
|---|---|
| `IValueObject` | Marker. Implement on a `record`, never a `class` |
| `ISingleValueObject<TValue>` | Just `Value`; for generic constraints |
| `ISingleValueObject<TValue, TSelf>` | Adds the `Create` (validates) / `Reconstruct` (does not) contract |
| `ILengthDefinedSingleValueObject` | Publishes `MinLength` / `MaxLength`; does not enforce them |

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
| `IDomainService<TReq>` | Rules spanning aggregates. Never starts a transaction |
| `IDomainService<TReq, TRes>` | Same, with a response |
| `IDomainServiceDTO` | DTO marker. **The only DTO that carries the session** |

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
| `ObjectNotFoundException` | A required object was missing. **Repositories never throw it** |
| `ObjectAlreadyExistException` | An object existed where there had to be none. Repositories never throw it |
| `DomainInvalidOperationException` | An operation the domain forbids |
| `ValueObjectInvalidException` | A value object invariant was broken; thrown from `Create` |
| `ValueObjectNullException` | The value was null |
| `ValueObjectLengthException` | Length out of range; carries `MinLength` / `MaxLength` / `CurrentLength` |
| `TransactionSessionNotFoundException` | A session was requested before it was begun |

---

## Rules that bite

The full list with wrong/right code is in [docs/best-practices.md](docs/best-practices.md).

1. **Never write `return null;` for `Optional<T>`.** The implicit conversion makes it `Some(null)`,
   whose `HasValue` is true — callers get true out of `TryGetValue` and then a
   `NullReferenceException`. Absence is `Optional<T>.Empty`
2. **Register `TransactionManager` as scoped.** It is not thread-safe and holds the in-flight sessions
3. **Never dispose the session in `ITransactionService`.** The manager owns it on every path
4. **Commits across multiple sessions are not atomic.** Not a two-phase commit coordinator
5. **Only `ICommandService` starts transactions**
6. **Repositories never throw `ObjectNotFoundException`.** Absence is a normal result
7. **Never add query methods to `IRepository`.** Listing and searching belong to `IQueryService`
8. **Validate in `Create`, not in `Reconstruct`.** `Reconstruct` restores data written under older rules

---

## Documentation

| Document | Contents |
|---|---|
| [docs/architecture.md](docs/architecture.md) | **Why `TSession` is passed everywhere**; differences from DDD / Clean Architecture |
| [docs/getting-started.md](docs/getting-started.md) | Zero to running, in 7 steps. DI registration included |
| [docs/best-practices.md](docs/best-practices.md) | Every rule, with wrong/right code |
| [docs/api-reference.md](docs/api-reference.md) | All 34 public types and the meaning of each type parameter |
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
    AppSession session, UserId identifier, CancellationToken cancellationToken = default);
```

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
public sealed record UserName : ISingleValueObject<string, UserName>, ILengthDefinedSingleValueObject
{
    private UserName(string value) => Value = value;   // private にして入口を 2 つに絞る

    public static int MaxLength => 16;
    public static int MinLength => 1;

    public string Value { get; }

    public static UserName Create(string value)        // 外部入力用。検証はここだけ
    {
        if (value is null)
        {
            throw new ValueObjectNullException($"{nameof(UserName)} に null は指定できません。");
        }
        if (value.Length < MinLength || value.Length > MaxLength)
        {
            // 引数が 3 つとも int。順番を間違えてもコンパイルが通るので名前付きで渡す
            throw new ValueObjectLengthException(
                minLength: MinLength, maxLength: MaxLength, currentLength: value.Length);
        }

        return new UserName(value);
    }

    public static UserName Reconstruct(string value) => new(value);   // 復元用。検証しない
}
```

`Reconstruct` が検証しないのは、ルールを厳しくした後でも古いデータを読み戻せるようにするためです。
`Validate` メンバーは存在しません（v2.0.1 で削除）。

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
}
```

等価性は「実行時型が同じ、かつ `Identifier` が等しい」。他のプロパティは見ないので、
名前を変えても同じユーザーのままです。

> 識別子も `UserId` のような値オブジェクトにするのが推奨です（→ [samples/](samples/)）。
> ここでは短くするため `Guid` のままにしています。

### 4. リポジトリ —— 不在は `Optional<T>.Empty`。`null` を返さない

```csharp
public sealed record OperateInfo(string OperatorId, DateTimeOffset OperatedAt);

public interface IUserRepository : IRepository<User, Guid, OperateInfo, AppSession>;

public sealed class UserRepository : IUserRepository
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

    // upsert。ObjectAlreadyExistException も ObjectNotFoundException も投げない
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
public sealed record CreateUserReq(string UserName, OperateInfo OperateInfo) : ICommandServiceDTO;

public sealed class CreateUserCommandService : ICommandService<CreateUserReq>
{
    private readonly ITransactionManager _transactionManager;
    private readonly IUserRepository _repository;

    public CreateUserCommandService(ITransactionManager transactionManager, IUserRepository repository)
        => (_transactionManager, _repository) = (transactionManager, repository);

    public ValueTask ExecuteAsync(CreateUserReq req, CancellationToken cancellationToken = default)
        => _transactionManager.ExecuteTransactionAsync<AppSession>(
            async (sessions, token) =>
            {
                var session = sessions.GetSession<AppSession>();

                // 外部入力はここで値オブジェクトに変換する。不正なら Create が例外を投げる
                var user = User.Create(Guid.NewGuid(), UserName.Create(req.UserName));
                await _repository.SaveAsync(session, user, req.OperateInfo, token);
            },
            cancellationToken: cancellationToken);
}
```

これより下の層はトランザクションを開始しません。
本体が例外を投げればロールバックされ、最後まで通ればコミットされます。

### 7. DI 登録 —— 最初につまずくところ

```csharp
var services = new ServiceCollection();

services.AddSingleton<UserStore>();

// TransactionManager は実行中のセッションを保持し、スレッドセーフではありません。
// Singleton にすると全リクエストでセッションが混線します。必ず Scoped で登録してください。
services.AddScoped<ITransactionManager, TransactionManager>();

// セッション型ごとに登録します。忘れると TransactionManager が InvalidOperationException を投げます。
services.AddScoped<ITransactionService<AppSession>, AppTransactionService>();

services.AddScoped<IUserRepository, UserRepository>();
services.AddScoped<ICommandService<CreateUserReq>, CreateUserCommandService>();

var provider = services.BuildServiceProvider();
```

### 8. 実行 —— 1 ユースケース = 1 スコープ

```csharp
using (var scope = provider.CreateScope())
{
    var createUser = scope.ServiceProvider.GetRequiredService<ICommandService<CreateUserReq>>();
    await createUser.ExecuteAsync(
        new CreateUserReq("alice", new OperateInfo("operator-1", DateTimeOffset.UtcNow)));
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
                IAggregateService / IDomainService
      ↑  実装する
Infrastructure  リポジトリ実装 / ITransactionService<TSession> / セッション型
```

## 主要コンポーネント

公開型は 34 個。詳細は各リンク先にあります。

**エンティティ** — [docs/domain-model.md](docs/domain-model.md)

| 型 | 概要 |
|---|---|
| `IEntity<TIdentifier>` | エンティティの契約。要求するのは `Identifier` だけ |
| `EntityBase<TSelf, TIdentifier>` | 基底クラス。等価性は「実行時型が同じ**かつ**識別子が等しい」 |

**値オブジェクト** — [docs/domain-model.md](docs/domain-model.md)

| 型 | 概要 |
|---|---|
| `IValueObject` | マーカー。`class` ではなく `record` で実装する |
| `ISingleValueObject<TValue>` | `Value` のみ。ジェネリック制約用 |
| `ISingleValueObject<TValue, TSelf>` | `Create`（検証あり）/ `Reconstruct`（検証なし）の規約が付く |
| `ILengthDefinedSingleValueObject` | `MinLength` / `MaxLength` を公開する。強制はしない |

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
| `IDomainService<TReq>` | 集約をまたぐルール。トランザクションを開始しない |
| `IDomainService<TReq, TRes>` | 戻り値がある版 |
| `IDomainServiceDTO` | DTO マーカー。**セッションを持つ唯一の DTO** |

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
| `ObjectNotFoundException` | 必要な対象が存在しなかった。**リポジトリは投げない** |
| `ObjectAlreadyExistException` | 存在してはいけない対象が存在した。リポジトリは投げない |
| `DomainInvalidOperationException` | ドメイン上許されない操作 |
| `ValueObjectInvalidException` | 値オブジェクトの不変条件違反。`Create` から投げる |
| `ValueObjectNullException` | 値が null だった |
| `ValueObjectLengthException` | 長さが範囲外。`MinLength` / `MaxLength` / `CurrentLength` を持つ |
| `TransactionSessionNotFoundException` | 開始されていないセッションを要求した |

## 必ず踏む地雷

正しい書き方と対にした一覧は [docs/best-practices.md](docs/best-practices.md) にあります。

1. **`Optional<T>` で `return null;` と書かない。** 暗黙変換によって `Some(null)` になり、
   `HasValue` が true のまま `TryGetValue` が true を返して `NullReferenceException` になります。
   不在は `Optional<T>.Empty`
2. **`TransactionManager` は Scoped で登録する。** スレッドセーフではなく、実行中のセッションを保持します
3. **`ITransactionService` の実装側でセッションを `Dispose` しない。** 所有権はマネージャーにあります
4. **複数セッションの commit はアトミックではない。** 2 相コミットではありません
5. **トランザクションを開始してよいのは `ICommandService` だけ**
6. **リポジトリは `ObjectNotFoundException` を投げない。** 不在は正常な結果です
7. **`IRepository` に検索系メソッドを足さない。** 一覧・条件検索は `IQueryService` の仕事
8. **検証は `Create` に書き、`Reconstruct` では検証しない**

## ドキュメント

| ドキュメント | 内容 |
|---|---|
| [docs/architecture.md](docs/architecture.md) | **なぜ `TSession` を引き回すのか**。DDD / クリーンアーキテクチャとの差分 |
| [docs/getting-started.md](docs/getting-started.md) | ゼロから動かすまでの 7 ステップ。DI 登録を含む |
| [docs/best-practices.md](docs/best-practices.md) | 規約の一覧。間違い → 正しい形 → なぜ |
| [docs/api-reference.md](docs/api-reference.md) | 公開型 34 個と型引数の意味 |
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
