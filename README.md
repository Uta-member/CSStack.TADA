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
- Optional: Utility struct to represent presence/absence of a value (None/Some(null)/Some(value)).

## Exceptions

- Custom exceptions based on TADAException (e.g., ObjectNotFoundException, DomainInvalidOperationException)

---

For a comprehensive usage guide, see:
- docs/USAGE.en.md (English)
- docs/USAGE.ja.md (日本語)

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
- Optional: 値の有無（None/Some(null)/Some(value)）を表現するユーティリティ構造体。

## 例外

- TADAException を基底とした独自例外群（例: ObjectNotFoundException, DomainInvalidOperationException）

---

詳細な使い方は以下を参照してください。
- docs/USAGE.en.md（英語）
- docs/USAGE.ja.md（日本語）
