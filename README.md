# CSStack.TADA

Transaction-Aware Domain Architecture（TADA）でシステム構築する際に便利な C#のクラス、インターフェース群です。

## 主な特徴

- ドメインサービス、リポジトリ、トランザクションサービスなど、TADA実装に必要なインターフェースや基底クラスを提供
- 型安全性を重視した設計
- .NET 8 対応

## 主要コンポーネントと使い方

### EntityBase

エンティティの基底クラスです。エンティティIDの型を指定して継承します。

### IRepository

リポジトリのインターフェースです。エンティティの取得や保存などを抽象化します。

### IDomainService

ドメインサービスのインターフェースです。ビジネスロジックを実装する際に利用します。

### AggregateServiceBase / IAggregateService

集約サービスの基底クラス・インターフェースです。リポジトリを利用した集約操作を実装します。

### ITransactionService

トランザクション管理のためのインターフェースです。UseCase層などで利用します。

### Optional

値の有無を表現するユーティリティクラスです。

## 例外

- `TADAException` を基底とした独自例外群を提供（例: `ObjectNotFoundException`, `DomainInvalidOperationException` など）

---

詳細なAPI仕様はソースコードのXMLドキュメントや各インターフェース・クラスのコメントを参照してください。
