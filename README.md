# CSStack.TADA

Transaction-Aware Domain Architecture Toolkit (TADA) – A library of C# classes and interfaces that help you implement TADA in your systems.

日本語の README は下部に続きます。

## Key Features

- Interfaces and base classes for TADA implementations (domain services, repositories, transaction services, etc.)
- Strongly-typed, safe design
- .NET 8 support

## Main Components and Usage

- EntityBase: Base class for entities; inherit with your strongly-typed ID. Equality is by identifier
  and run-time type. See [docs/domain-model.md](docs/domain-model.md).
- IValueObject / ISingleValueObject: Value object markers and the `Create` (validates) / `Reconstruct`
  (restores from storage) contract. Implement them on a `record`.
- IRepository: Repository interface; abstracts persistence operations. `SaveAsync` is an upsert, and
  there are deliberately no query methods — those belong to IQueryService.
- IDomainService: Domain service interfaces for business logic that spans aggregates.
- AggregateServiceBase / IAggregateService: Base/interface for aggregate services using repositories.
- ICommandService / IQueryService: Use case entry points. A command service owns the transaction
  boundary. See [docs/use-case.md](docs/use-case.md).
- ITransactionService: Interface for transaction management; used in the Use Case layer.
- ITransactionManager / TransactionManager: Runs a unit of work across one or more transaction sessions.
- Optional: Utility struct representing three states (None/Some(null)/Some(value)). See [docs/optional.md](docs/optional.md).

## Documentation

- [docs/domain-model.md](docs/domain-model.md) — entities, value objects, repositories, aggregates:
  what TADA enforces and what is convention
- [docs/use-case.md](docs/use-case.md) — command / query / domain services and where the transaction begins
- [docs/optional.md](docs/optional.md) — the three states of `Optional<T>`

## Transaction management

```csharp
// Registration. TransactionManager holds the sessions of the transaction in flight and is NOT
// thread-safe: it must be registered as scoped.
services.AddScoped<ITransactionManager, TransactionManager>();
services.AddScoped<ITransactionService<MySession>, MyTransactionService>();

// Usage.
await transactionManager.ExecuteTransactionAsync<MySession>(
    async (sessions, cancellationToken) =>
    {
        var session = sessions.GetSession<MySession>();
        await repository.SaveAsync(session, entity, operateInfo, cancellationToken);
    },
    cancellationToken: cancellationToken);
```

Three things to know before you rely on it:

- **Sessions are owned by the manager.** It disposes every session it began, after commit, after
  rollback and on any error path. `ITransactionService<TSession>` implementations must not dispose them.
- **Commits across multiple sessions are not atomic.** This is not a two-phase commit coordinator.
  If the second session fails to commit, the first one stays committed; the manager only rolls back the
  sessions that have not been committed yet.
- **Rollback is never cancelled.** Even when the `CancellationToken` is already cancelled, the rollback
  runs, so cancellation cannot leave transactions open.

## Optional&lt;T&gt;

`Optional<TValue>` represents **three** states — `None` / `Some(null)` / `Some(value)` — so that
"not specified" can be told apart from "explicitly set to null", which is what partial updates such
as HTTP PATCH need. It is the return type of `IRepository.FindByIdentifierAsync`.

```csharp
// Repository. "Not found" is Optional<T>.Empty — never `return null;`.
// The implicit conversion turns null into Some(null), whose HasValue is true, so callers
// get true out of TryGetValue and then a NullReferenceException.
public async ValueTask<Optional<User>> FindByIdentifierAsync(
    MySession session,
    UserId identifier,
    CancellationToken cancellationToken = default)
{
    var record = await session.Users.FindAsync(identifier.Value, cancellationToken);
    return record is null ? Optional<User>.Empty : User.Reconstruct(record);
}

// Caller.
var displayName = optional.Match(
    onSome: user => user.Name,
    onNone: () => "(not registered)");
```

- **Declare the nullability of the type argument.** `Optional<string?>` when null is a legal value,
  `Optional<User>` when it is not. The compiler's nullable analysis follows that declaration.
- Equality distinguishes all three states: `None == Some(null)` is **false**.
- `Map` / `Select` / `Bind` / `SelectMany` / `Where` / `Match` are provided, so LINQ query syntax works.

See [docs/optional.md](docs/optional.md) for the full guide.

## Exceptions

- Custom exceptions based on TADAException (e.g., ObjectNotFoundException, DomainInvalidOperationException)

## Changelog

See [CHANGELOG.md](CHANGELOG.md). v3.0.0 contains breaking changes to the transaction APIs.

---

# CSStack.TADA（日本語）

Transaction-Aware Domain Architecture（TADA）でシステム構築する際に役立つ C# のクラス・インターフェース群です。

## 主な特徴

- ドメインサービス、リポジトリ、トランザクションサービスなど、TADA 実装に必要なインターフェースや基底クラスを提供
- 型安全性を重視した設計
- .NET 8 対応

## 主要コンポーネントと使い方

- EntityBase: エンティティの基底クラス。強い型付けの ID を指定して継承します。等価性は識別子と
  実行時型で判断されます。[docs/domain-model.md](docs/domain-model.md) を参照。
- IValueObject / ISingleValueObject: 値オブジェクトのマーカーと `Create`（検証あり）/
  `Reconstruct`（永続化からの復元・検証なし）の規約。`record` で実装してください。
- IRepository: リポジトリのインターフェース。永続化処理を抽象化します。`SaveAsync` は upsert で、
  検索系メソッドは意図的に持たせていません（それらは IQueryService の仕事です）。
- IDomainService: 集約をまたぐドメインのルールを実装するためのインターフェース。
- AggregateServiceBase / IAggregateService: リポジトリを利用した集約操作のための基底クラス/インターフェース。
- ICommandService / IQueryService: ユースケースの入口。トランザクションの境界はコマンドサービスにあります。
  [docs/use-case.md](docs/use-case.md) を参照。
- ITransactionService: トランザクション管理のためのインターフェース。ユースケース層等で利用します。
- ITransactionManager / TransactionManager: 1 つ以上のトランザクションセッションをまたいで処理を実行します。
- Optional: 三状態（None/Some(null)/Some(value)）を表現するユーティリティ構造体。[docs/optional.md](docs/optional.md) を参照。

## ドキュメント

- [docs/domain-model.md](docs/domain-model.md) — エンティティ / 値オブジェクト / リポジトリ / 集約。
  TADA が何を強制し、何を規約に留めているか
- [docs/use-case.md](docs/use-case.md) — コマンド / クエリ / ドメインサービスの使い分けと
  トランザクションの境界
- [docs/optional.md](docs/optional.md) — `Optional<T>` の三状態

## トランザクション管理

```csharp
// 登録。TransactionManager は実行中のトランザクションのセッションを保持するためスレッドセーフではありません。
// 必ず Scoped で登録してください。
services.AddScoped<ITransactionManager, TransactionManager>();
services.AddScoped<ITransactionService<MySession>, MyTransactionService>();

// 利用側。
await transactionManager.ExecuteTransactionAsync<MySession>(
    async (sessions, cancellationToken) =>
    {
        var session = sessions.GetSession<MySession>();
        await repository.SaveAsync(session, entity, operateInfo, cancellationToken);
    },
    cancellationToken: cancellationToken);
```

利用前に押さえておくべき点が 3 つあります。

- **セッションの所有権は TransactionManager にあります。** commit 後・rollback 後・例外時のいずれの経路でも
  TransactionManager が `Dispose` します。`ITransactionService<TSession>` の実装側で `Dispose` してはいけません。
- **複数セッションの commit はアトミックではありません。** 2 相コミットではないため、2 つ目の commit が
  失敗しても 1 つ目は確定したままです。TransactionManager は未 commit のセッションのみロールバックを試みます。
- **ロールバックはキャンセルされません。** `CancellationToken` がキャンセル済みでもロールバックは実行されるため、
  キャンセルによってトランザクションが開いたまま残ることはありません。

## Optional&lt;T&gt;

`Optional<TValue>` は `None` / `Some(null)` / `Some(value)` の **三状態**を表現します。
「未指定」と「明示的に null を指定した」を区別するためで、HTTP PATCH のような部分更新で必要になります。
`IRepository.FindByIdentifierAsync` の戻り値でもあります。

```csharp
// リポジトリ実装。「見つからなかった」は Optional<T>.Empty で表します。`return null;` は禁止です。
// 暗黙変換によって null は Some(null)（HasValue = true）になるため、呼び出し側の
// TryGetValue が true を返したうえで NullReferenceException になります。
public async ValueTask<Optional<User>> FindByIdentifierAsync(
    MySession session,
    UserId identifier,
    CancellationToken cancellationToken = default)
{
    var record = await session.Users.FindAsync(identifier.Value, cancellationToken);
    return record is null ? Optional<User>.Empty : User.Reconstruct(record);
}

// 呼び出し側。
var displayName = optional.Match(
    onSome: user => user.Name,
    onNone: () => "(未登録)");
```

- **型引数の null 許容性を正しく宣言してください。** null が値として正当なら `Optional<string?>`、
  そうでなければ `Optional<User>` です。コンパイラの null 許容解析はこの宣言に従います。
- 等価性は三状態を区別します。`None == Some(null)` は **false** です。
- `Map` / `Select` / `Bind` / `SelectMany` / `Where` / `Match` を備えており、LINQ クエリ構文が使えます。

詳細は [docs/optional.md](docs/optional.md) を参照してください。

## 例外

- TADAException を基底とした独自例外群（例: ObjectNotFoundException, DomainInvalidOperationException）

## 変更履歴

[CHANGELOG.md](CHANGELOG.md) を参照してください。v3.0.0 にはトランザクション周りの破壊的変更が含まれます。

