# CSStack.TADA

Transaction-Aware Domain Architecture Toolkit (TADA) – A library of C# classes and interfaces that help you implement TADA in your systems.

日本語の README は下部に続きます。

## Key Features

- Interfaces and base classes for TADA implementations (domain services, repositories, transaction services, etc.)
- Strongly-typed, safe design
- .NET 8 support

## Main Components and Usage

- EntityBase: Base class for entities; inherit with your strongly-typed ID.
- IRepository: Repository interface; abstracts persistence operations.
- IDomainService: Domain service interfaces for business logic.
- AggregateServiceBase / IAggregateService: Base/interface for aggregate services using repositories.
- ITransactionService: Interface for transaction management; used in the Use Case layer.
- ITransactionManager / TransactionManager: Runs a unit of work across one or more transaction sessions.
- Optional: Utility struct to represent presence/absence of a value (None/Some(null)/Some(value)).

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
        await repository.SaveAsync(entity, session, cancellationToken);
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

- EntityBase: エンティティの基底クラス。強い型付けの ID を指定して継承します。
- IRepository: リポジトリのインターフェース。永続化処理を抽象化します。
- IDomainService: ビジネスロジックを実装するためのドメインサービスのインターフェース。
- AggregateServiceBase / IAggregateService: リポジトリを利用した集約操作のための基底クラス/インターフェース。
- ITransactionService: トランザクション管理のためのインターフェース。ユースケース層等で利用します。
- ITransactionManager / TransactionManager: 1 つ以上のトランザクションセッションをまたいで処理を実行します。
- Optional: 値の有無（None/Some(null)/Some(value)）を表現するユーティリティ構造体。

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
        await repository.SaveAsync(entity, session, cancellationToken);
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

## 例外

- TADAException を基底とした独自例外群（例: ObjectNotFoundException, DomainInvalidOperationException）

## 変更履歴

[CHANGELOG.md](CHANGELOG.md) を参照してください。v3.0.0 にはトランザクション周りの破壊的変更が含まれます。

