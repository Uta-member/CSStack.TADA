# API リファレンス

公開型は **35 個**。すべてフラットな `CSStack.TADA` namespace にあるので、
`using CSStack.TADA;` の 1 行で全部使える。

このページは「どの型が何のためにあるか」と「型引数の意味」の索引。
使い方の解説は各リンク先にある。

- [architecture.md](architecture.md) — 設計思想
- [getting-started.md](getting-started.md) — 手順
- [best-practices.md](best-practices.md) — 規約
- [domain-model.md](domain-model.md) / [use-case.md](use-case.md) / [optional.md](optional.md) — 詳細

---

## 一覧

| 分類 | 型 |
|---|---|
| エンティティ | [`IEntity<TIdentifier>`](#ientitytidentifier) / [`EntityBase<TSelf, TIdentifier>`](#entitybasetself-tidentifier) |
| 値オブジェクト | [`IValueObject`](#ivalueobject) / [`ISingleValueObject<TValue>`](#isinglevalueobjecttvalue) / [`ISingleValueObject<TValue, TSelf>`](#isinglevalueobjecttvalue-tself) / [`ILengthDefinedSingleValueObject`](#ilengthdefinedsinglevalueobject) |
| リポジトリ | [`IRepository<...>`](#irepositorytentity-tentityidentifier-toperateinfo-tsession) / [`IRepositoryDeletable<...>`](#irepositorydeletabletentity-tentityidentifier-toperateinfo-tsession) |
| 集約サービス | [`IAggregateService<...>`](#iaggregateservicetentity-tentityidentifier-trepository-toperateinfo-tsession) / [`AggregateServiceBase<...>`](#aggregateservicebasetentity-tentityidentifier-trepository-toperateinfo-tsession) |
| ドメインサービス | [`IDomainService<TReq>`](#idomainservicetreq) / [`IDomainService<TReq, TRes>`](#idomainservicetreq-tres) / [`IDomainServiceDTO`](#idomainservicedto) |
| コマンドサービス | [`ICommandService<TReq>`](#icommandservicetreq) / [`ICommandService<TReq, TRes>`](#icommandservicetreq-tres) / [`ICommandServiceDTO`](#icommandservicedto) |
| クエリサービス | [`IQueryService<TReq, TRes>`](#iqueryservicetreq-tres) / [`IQueryService<TRes>`](#iqueryservicetres) / [`IQueryServiceDTO`](#iqueryservicedto) |
| トランザクション | [`ITransactionManager`](#itransactionmanager) / [`TransactionManager`](#transactionmanager) / [`TransactionSessions`](#transactionsessions) / [`ITransactionService`](#itransactionservice) / [`ITransactionService<TSession>`](#itransactionservicetsession) |
| ユーティリティ | [`Optional<TValue>`](#optionaltvalue) / [`OptionalExtensions`](#optionalextensions) |
| 例外 | [`TADAException`](#tadaexception) 以下 9 個（[例外](#例外)） |

---

## 型引数の意味

**どの型でも同じ名前は同じ意味。** ここだけ覚えれば型引数 5 個も読める。

| 型引数 | 意味 | 制約 | 例 |
|---|---|---|---|
| `TEntity` | 集約のエンティティ。1 集約に 1 つ | `IEntity<TEntityIdentifier>` | `User` |
| `TEntityIdentifier` | エンティティの識別子。値オブジェクト推奨 | `notnull` | `UserId` |
| `TOperateInfo` | **書き込みと一緒に記録する「誰が・いつ」。** 読み取り系は受け取らない | `notnull` | `OperateInfo` |
| `TSession` | **トランザクションセッション。** 呼び出し側が渡す。実装側は begin / commit / dispose しない | `IDisposable` | `TSession` のまま開く（後述） |
| `TRepository` | 集約のリポジトリ。1 集約に 1 つ | `IRepository<...>` | `IUserRepository<TSession>` |
| `TReq` | リクエスト DTO。**そのサービスの口の中にネストする** | 各 `~DTO` マーカー | `ICreateUserCommandService.Req` |
| `TRes` | レスポンス DTO。**同上** | 各 `~DTO` マーカー | `ICreateUserCommandService.Res` |
| `TSelf` | **自分自身の型**（CRTP）。実装する型をそのまま渡す | 各インターフェース | `record UserId : ISingleValueObject<Guid, UserId>` |

`TOperateInfo` と `TSession` の詳しい説明は [domain-model.md](domain-model.md#toperateinfo-は誰がいつ) と
[architecture.md](architecture.md#なぜ-tsession-を全レイヤーに引き回すのか)。

**`TSession` に具体型を渡すのはインフラ層の実装とプレゼンテーション層の DI 登録だけ。**
ドメイン層・ユースケース層の型定義では `TSession` のまま開いておき、ユースケースと
ドメインサービスでは名前を `T[集約名]Session`（`TUserSession` など）にする
（→ [architecture.md](architecture.md#ドメイン層に具体的なセッション型を書かない)）。

---

## エンティティ

### `IEntity<TIdentifier>`

エンティティのインターフェース。要求するのは `Identifier` プロパティ 1 つだけ。

```csharp
TIdentifier Identifier { get; }
```

通常は `EntityBase` を継承する。別の基底クラスが必要なときだけ直接実装し、
そのときは識別子による等価性を自分で実装する。

### `EntityBase<TSelf, TIdentifier>`

エンティティの基底クラス。**識別子による等価性**を実装済み。

```csharp
public sealed class User : EntityBase<User, UserId>
```

- 等価性は「**実行時型が同じ、かつ `Identifier` が等しい**」。他のプロパティは見ない
- 基底クラスを共有する `Admin` と `Guest` は、識別子が同じでも等価にならない
- `==` / `!=` / `Equals` / `GetHashCode` を提供する
- `Create` / `Reconstruct` は**強制されない**（値オブジェクトと違う点）が、揃えることを推奨

→ [domain-model.md](domain-model.md#エンティティ)

---

## 値オブジェクト

### `IValueObject`

値オブジェクトのマーカー。メンバーは無い。

**`record` で実装すること。** `class` だと参照等価のままになり、
同じ値を持つインスタンスが等価にならない。マーカーなのでこれを強制できない。

`ValueObjectBase` は v2.0.0 で削除された（`record` のほうが適切なため）。

### `ISingleValueObject<TValue>`

単一の値を包む値オブジェクト。`Value` プロパティのみ。

「`TValue` を包む値オブジェクトなら何でも」をジェネリック制約で受けたいときに使う。
型引数 2 個のほうは `static abstract` メンバーを持つため、制約に使うと具体型も名指しする必要がある。

### `ISingleValueObject<TValue, TSelf>`

**値オブジェクトを宣言するときはこちら。** `Create` / `Reconstruct` の規約が付く。

```csharp
static abstract TSelf Create(TValue value);       // 検証あり。外部入力用
static abstract TSelf Reconstruct(TValue value);  // 検証なし。永続化からの復元用
TValue Value { get; }
```

- **検証は `Create` の中だけ。** `Validate` メンバーは存在しない（v2.0.0 で削除）
- **`Reconstruct` が検証しないのは意図的。** ルールを厳しくした後でも古いデータを読み戻せるようにするため
- コンストラクタは private にする
- 不正な値は `ValueObjectInvalidException`（またはその派生）を投げる。sentinel を返さない

`static abstract` を使うため **C# 11 以上が必要**。

### `ILengthDefinedSingleValueObject`

長さの上下限を**公開する**インターフェース。型引数は無い。

```csharp
static abstract int MaxLength { get; }
static abstract int MinLength { get; }
```

**公開するだけで強制はしない。** 強制するのは `Create`。
インスタンスを作らずに読めるので、画面側が `UserName.MaxLength` を
そのまま `maxlength` に使える（同じ数字を 2 箇所に書かなくて済む）。

→ [domain-model.md](domain-model.md#値オブジェクト)

---

## リポジトリ

### `IRepository<TEntity, TEntityIdentifier, TOperateInfo, TSession>`

エンティティを識別子で出し入れする。

```csharp
ValueTask<Optional<TEntity>> FindByIdentifierAsync(
    TSession session, TEntityIdentifier identifier, CancellationToken cancellationToken = default);

ValueTask SaveAsync(
    TSession session, TEntity entity, TOperateInfo operateInfo,
    CancellationToken cancellationToken = default);
```

- **`FindByIdentifierAsync` は不在を `Optional<T>.Empty` で返す。** `return null;` は
  `Some(null)` になるので禁止。`ObjectNotFoundException` も投げない
- **`SaveAsync` は upsert。** 「既に居る / 居ない」で失敗させない。
  `ObjectAlreadyExistException` も `ObjectNotFoundException` も投げない
- **検索系メソッドを足さない。** 一覧・条件検索は `IQueryService`
- セッションは引数で受け取るだけ。begin / commit / dispose しない
- **`TSession` に具体型を渡さない。** 派生インターフェースも
  `IUserRepository<TSession>` のように開いたままにし、閉じるのは実装（インフラ層）
- 書き込みが確定するのは `ITransactionManager` が commit したときで、メソッドが戻った時点ではない

### `IRepositoryDeletable<TEntity, TEntityIdentifier, TOperateInfo, TSession>`

`IRepository` に削除を足したもの。

```csharp
ValueTask DeleteAsync(
    TSession session, TEntity entity, TOperateInfo operateInfo,
    CancellationToken cancellationToken = default);
```

識別子ではなく**エンティティ**を受け取るのは、呼び出し側が先に読んで
存在を確認している前提のため。既に居ないものを削除しても失敗にしない。

→ [domain-model.md](domain-model.md#リポジトリ)

---

## 集約サービス

### `IAggregateService<TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession>`

```csharp
ValueTask<Optional<TEntity>> GetEntityByIdentifierAsync(
    TSession session, TEntityIdentifier identifier, CancellationToken cancellationToken = default);
```

**型引数 5 個は削り忘れではない。** 「この集約にはエンティティが 1 つ、
その上のリポジトリが 1 つ、エンティティに到達できるサービスが 1 つ」という
TADA の集約の定義を型で書いている。

不在は `Optional<T>.Empty` を返す。`ObjectNotFoundException` に変えるのは、
エンティティの存在を要求する具体的なメソッドの側。

**これを継承した集約サービスのインターフェースを宣言し、その実装を
`AggregateServiceBase` の派生クラスとして書く。** 上の層に注入するのはインターフェース側。

```csharp
public interface IUserAggregateService<TSession>
    : IAggregateService<User, UserId, IUserRepository<TSession>, OperateInfo, TSession>
    where TSession : IDisposable
{
    ValueTask RenameAsync(
        TSession session, UserId identifier, UserName newName, OperateInfo operateInfo,
        CancellationToken ct = default);
}
```

**この口は実質的に集約ルート。並べるのはドメインの操作だけで、`SaveAsync` のような
汎用的な操作は置かない。** 「取得する → 変更する → 保存する」を 1 つの操作に閉じ、
エンティティを上の層へ出さない
→ [best-practices.md](best-practices.md#13-集約サービスに-saveasync-のような汎用的な操作を置かない)

### `AggregateServiceBase<TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession>`

`IAggregateService` の基底クラス。実装済みなのは `GetEntityByIdentifierAsync`
（リポジトリへの委譲）だけで、`protected readonly TRepository Repository` を派生クラスに公開する。

集約のルール（保存・削除・不在時の扱い）は派生クラスに書く。

```csharp
public sealed class UserAggregateService<TSession>
    : AggregateServiceBase<User, UserId, IUserRepository<TSession>, OperateInfo, TSession>,
    IUserAggregateService<TSession>
    where TSession : IDisposable
```

- `TRepository` と `TSession` に具体型を渡さない。具体型が決まるのは DI 登録
  （プレゼンテーション層）
  → [architecture.md](architecture.md#ドメイン層に具体的なセッション型を書かない)
- **この基底クラスは実装の詳細。** 上の層には
  `IAggregateService` を継承した自前のインターフェースを見せる
  → [best-practices.md](best-practices.md#12-集約サービスとユースケースはインターフェースを立ててから実装する)

---

## ドメインサービス

### `IDomainService<TReq>`

集約をまたぐルール、またはエンティティに持たせるべきでないルール。

```csharp
ValueTask ExecuteAsync(TReq req, CancellationToken cancellationToken = default);
```

- **トランザクションを開始しない。** `ITransactionManager` を注入してはいけない
- **セッション引数が無いので、DTO にセッションを載せて渡す**
- 1 エンティティで完結するルールはエンティティ自身に、
  1 集約で完結するなら集約サービスに、オーケストレーションはコマンドサービスに置く
- **専用の口を立て、リクエストをその中にネストする**

```csharp
public interface IUserNameUniquenessService<TUserSession>
    : IDomainService<IUserNameUniquenessService<TUserSession>.Req>
    where TUserSession : IDisposable
{
    sealed record Req(TUserSession Session, UserName Name, UserId ExceptUserId)
        : IDomainServiceDTO;
}
```

### `IDomainService<TReq, TRes>`

戻り値があるドメインサービス。契約は同じ。

### `IDomainServiceDTO`

ドメインサービスの DTO マーカー。`record` で宣言し、そのドメインサービスの口の中にネストする。
**3 種の DTO のうち、これだけがセッションを持つ。** エンティティや値オブジェクトも持ってよい。

→ [use-case.md](use-case.md#3-種のサービスの使い分け)

---

## コマンドサービス

### `ICommandService<TReq>`

状態を変えるユースケース 1 つ。**トランザクションの境界。**

```csharp
ValueTask ExecuteAsync(TReq req, CancellationToken cancellationToken = default);
```

`ITransactionManager` を注入して `ExecuteTransactionAsync` で包み、
取り出したセッションを下の層へ渡す。**これより下の層はトランザクションを開始しない。**

規約であって強制ではない（基底クラスは無い）が、`ITransactionManager` に
まったく触らないコマンドサービスはほぼ間違い。

**これを継承した、セッション型引数を持たないインターフェースをユースケースごとに立て、
リクエストとレスポンスをその中にネストする。**
実装はセッション型を型引数に持つので、この口が無いとプレゼンテーション層が
`CreateUserCommandService<AppSession>` を名指しすることになる。

```csharp
public interface ICreateUserCommandService
    : ICommandService<ICreateUserCommandService.Req, ICreateUserCommandService.Res>
{
    sealed record Req(string UserName, OperateInfo OperateInfo) : ICommandServiceDTO;

    sealed record Res(Guid UserId) : ICommandServiceDTO;
}

public sealed class CreateUserCommandService<TUserSession> : ICreateUserCommandService
    where TUserSession : IDisposable { /* ... */ }
```

→ [best-practices.md](best-practices.md#12-集約サービスとユースケースはインターフェースを立ててから実装する)、
[best-practices.md](best-practices.md#14-リクエスト--レスポンスはサービスの口の中にネストする)

### `ICommandService<TReq, TRes>`

採番した識別子などを返すコマンドサービス。

**レスポンスにエンティティを入れない。** エンティティは読み出したトランザクションのもので、
呼び出し側に届く頃にはセッションは `Dispose` 済み。

### `ICommandServiceDTO`

コマンドサービスの DTO マーカー。`record` で宣言し、そのユースケースの口の中に
`Req` / `Res` としてネストする。
アプリケーションの境界に立つので、素の引数と操作情報だけを持つ。
**セッションもエンティティも持たない。**

値オブジェクトへの変換はコマンドサービスの仕事で、
不正な入力は `ValueObjectInvalidException` で報告される。

→ [use-case.md](use-case.md#コマンドサービスがトランザクションの境界)

---

## クエリサービス

### `IQueryService<TReq, TRes>`

読み取り専用のユースケース 1 つ。**引数があるときはこちら。**

```csharp
ValueTask<TRes> ExecuteAsync(TReq req, CancellationToken cancellationToken = default);
```

- **リポジトリ・エンティティ・集約サービスを通さず、ストアを直接読んで DTO を返す**
- 読み取りに通常トランザクションは要らないので、`ITransactionManager` を注入せず
  自前の接続を持つ
- 実行中のトランザクションの未コミット状態を読む必要があるときだけ、
  リクエスト DTO にセッションを載せる（2 つ目のトランザクションを開始しない）
- **クエリごとに口を立て、DTO をその中にネストする。** セッション型引数は無いので理由は
  コマンドサービスとは違い、レスポンス型をそのクエリに固定するため

```csharp
public interface ISearchUsersQueryService
    : IQueryService<ISearchUsersQueryService.Req, ISearchUsersQueryService.Res>
{
    sealed record Req(string NamePrefix) : IQueryServiceDTO;

    sealed record Res(IReadOnlyList<UserSummary> Users) : IQueryServiceDTO;
}
```

### `IQueryService<TRes>`

引数を取らないクエリサービス。

> **型引数 1 個は「レスポンス」。** `ICommandService<TReq>` /
> `IDomainService<TReq>` の 1 個目がリクエストなのと逆になっている。
> `IQueryService<Foo>` は「`Foo` を返す」、`ICommandService<Foo>` は「`Foo` を受け取る」。
> 引数があるときは迷わず `IQueryService<TReq, TRes>` を使えばよい。

```csharp
ValueTask<TRes> ExecuteAsync(CancellationToken cancellationToken = default);
```

### `IQueryServiceDTO`

クエリサービスの DTO マーカー。`record` で宣言し、そのクエリの口の中にネストする
（複数のクエリで共有する読み取りモデルだけは外に置く）。
呼び出し側の都合に合わせた素のデータを持つ。**エンティティを返さない。**

→ [use-case.md](use-case.md#クエリサービスはリポジトリを通さない)

---

## トランザクション

### `ITransactionManager`

1 つ以上のセッションをまたいで処理を実行する。

**主に使うのは `ExecuteTransactionAsync`。** begin / commit / rollback を個別に
呼ぶメソッドもあるが、通常は必要ない。

```csharp
// 型引数でセッション型を指定する（1〜3 個のオーバーロードがある）
ValueTask ExecuteTransactionAsync<TSession1>(
    Func<TransactionSessions, CancellationToken, ValueTask> transactionFunction,
    Func<Exception, ValueTask>? beforeRollbackHandler = null,
    CancellationToken cancellationToken = default);

// 4 個以上、または実行時に決まるとき
ValueTask ExecuteTransactionAsync(
    ImmutableList<Type> sessionTypes,
    Func<TransactionSessions, CancellationToken, ValueTask> transactionFunction,
    Func<Exception, ValueTask>? beforeRollbackHandler = null,
    CancellationToken cancellationToken = default);
```

| メンバー | 用途 |
|---|---|
| `ExecuteTransactionAsync<TSession1>` 〜 `<TSession1, TSession2, TSession3>` | begin → 本体 → commit を一括で行う。**通常はこれ** |
| `ExecuteTransactionAsync(ImmutableList<Type>, ...)` | セッション型が 4 個以上、または実行時に決まる場合 |
| `BeginTransactionAsync<TSession>()` / `BeginTransactionAsync(Type)` | 個別に開始。開始済みなら何もしない |
| `BeginTransactionsAsync(ImmutableList<Type>)` | 複数をまとめて開始。途中で失敗したら開始済みを rollback + dispose して再スロー |
| `CommitTransactionsAsync()` | 開始順に commit し、全部 dispose する |
| `RollbackTransactionsAsync()` | 逆順に rollback し、全部 dispose する |
| `GetSession<TSession>()` / `GetSession(Type)` | 開始済みセッションを取得。無ければ `TransactionSessionNotFoundException` |
| `TryGetSession<TSession>(out TSession)` | 例外を投げない版 |
| `GetTransactionService<TSession>()` / `GetTransactionService(Type)` | 登録済みの `ITransactionService` を取得 |

`beforeRollbackHandler` は失敗したときに呼ばれる。診断情報の採取に使う。
ここで例外を投げてもロールバックは止まらない（例外は元の失敗と一緒に報告される）。

**呼ばれる位置は「何が失敗したか」で変わる。**

| 失敗した場所 | 呼ばれる位置 | ハンドラから見たセッション |
|---|---|---|
| 本体（`transactionFunction`）／ begin | ロールバック**前** | まだ生きている。データを観測できる |
| commit | ロールバック**後** | **既に `Dispose` 済み** |

commit 失敗の経路が後になるのは、`CommitTransactionsAsync` が内部でロールバックと
`Dispose` を済ませてから throw するため。**セッションを触る診断コードは、
この経路では Dispose 済みであることを前提に書く**（そこで出た例外も
元の失敗と一緒に報告されるので握り潰されはしない）。

**契約:**

- **入れ子にできない。** 実行中の本体（または `beforeRollbackHandler`）から同じマネージャーの
  `ExecuteTransactionAsync` を呼ぶと `NestedTransactionException`。連続して呼ぶのは正当
- **セッションの所有権はマネージャーにある。** commit 後・rollback 後・例外時の
  いずれの経路でも `Dispose` する
- **複数セッションの commit はアトミックではない。** 2 相コミットではない
- **ロールバックはキャンセルされない。** `CancellationToken` がキャンセル済みでも実行される
- 失敗が 1 つならそのまま再スローされ、複数なら `AggregateException` になる
  （最初の内部例外が元の失敗）

### `TransactionManager`

`ITransactionManager` の既定の実装。`IServiceProvider` から
`ITransactionService<TSession>` を解決する。

```csharp
services.AddScoped<ITransactionManager, TransactionManager>();
```

- **必ず Scoped で登録する。** 実行中のセッションを可変フィールドに保持し、
  スレッドセーフではない。Singleton にすると全リクエストで混線する
- 1 インスタンスを複数スレッドや並行 `Task` から同時に動かしてもいけない
- `ITransactionService<TSession>` が登録されていないと `InvalidOperationException`

### `TransactionSessions`

`ExecuteTransactionAsync` の本体に渡されるセッションの集合。

```csharp
TSession GetSession<TSession>();                              // 無ければ例外
bool TryGetSession<TSession>(out TSession session);           // 例外を投げない版
IReadOnlyDictionary<Type, IDisposable> Sessions { get; }
```

**渡された呼び出しの中でのみ有効。** セッションはマネージャーが `Dispose` するので、
このオブジェクトをキャプチャして後で使ってはいけない。呼び出し開始時点の**コピー**なので、
commit 後も同じセッションを指し続ける（＝ Dispose 済みのセッションを返す）。

`ExecuteTransactionAsync` に渡していないセッション型を要求すると
`TransactionSessionNotFoundException`。本体の中で
`ITransactionManager.BeginTransactionAsync<TOther>()` を呼んで別のセッションを開始しても、
この集合からは取得できない（マネージャー側の `GetSession<TOther>()` からは取得できる）。

### `ITransactionService`

非ジェネリック版。**直接実装しない。**

`ITransactionManager` が実行時にしか分からないセッション型を `dynamic` 無しで
扱えるようにするためだけに存在する。`ITransactionService<TSession>` が
明示的実装を提供している。

### `ITransactionService<TSession>`

セッション型ごとに 1 つ実装する。

```csharp
ValueTask<TSession> BeginAsync(CancellationToken cancellationToken = default);
ValueTask CommitAsync(TSession session, CancellationToken cancellationToken = default);
ValueTask RollbackAsync(TSession session, CancellationToken cancellationToken = default);
```

**セッションを `Dispose` してはいけない。** 所有権は `ITransactionManager` にある。

DI への登録が必須。忘れると `TransactionManager` が `InvalidOperationException` を投げる。

```csharp
services.AddScoped<ITransactionService<AppSession>, AppTransactionService>();
```

---

## ユーティリティ

### `Optional<TValue>`

`None` / `Some(null)` / `Some(value)` の **三状態**を表す `readonly struct`。
「未指定」と「明示的に null」を区別するためにあり、HTTP PATCH のような部分更新で必要になる。

`IRepository.FindByIdentifierAsync` の戻り値でもある。

| メンバー | 説明 |
|---|---|
| `Optional<T>.Empty` | **`None`。不在はこれを返す** |
| `Optional<T>.Some(value)` | `Some(value)`。null を渡せば `Some(null)` |
| `new Optional<T>()` | `None` |
| `new Optional<T>(value)` | `Some(value)`。null を渡せば `Some(null)` |
| `new Optional<T>(value, hasValue)` | `hasValue` で状態を明示する |
| `HasValue` | `Some` なら true。**`Some(null)` でも true** |
| `Value` | `None` なら `default`。`Some(null)` でも null なので、これだけでは区別できない |
| `TryGetValue(out T)` | 取り出す標準的な方法 |
| `GetValue(defaultValue)` / `GetValueOrDefault(defaultValue)` | `None` のときだけ既定値。**`Some(null)` は null が返る**（同じ実装の別名） |
| `Match(onSome, onNone)` | 分岐。`Action` 版と戻り値のある `Func` 版がある |
| `==` / `!=` / `Equals` / `GetHashCode` | 三状態を区別する。**`None == Some(null)` は false** |
| `implicit operator Optional<T>(T)` | **`return null;` が `Some(null)` になる原因** |

**`return null;` と書かない。** 不在は `Optional<T>.Empty`。

型引数の null 許容性も正しく宣言する（null が正当なら `Optional<string?>`、
そうでなければ `Optional<User>`）。

→ [optional.md](optional.md)

### `OptionalExtensions`

`Optional<T>` の拡張メソッド。LINQ クエリ構文もこれで使えるようになる。

| メソッド | 説明 |
|---|---|
| `Map` / `Select` | `Some` のときだけ変換する |
| `Bind` / `SelectMany` | `Optional` を返す関数に繋ぐ（`SelectMany` は 3 引数版もある） |
| `Where` | 条件を満たさなければ `None` にする |
| `Exchange` | 別の `Optional` へ変換する |
| `CreateSingleValueObject` | 中身を `ISingleValueObject.Create` に通す（**検証あり**） |
| `ReconstructSingleValueObject` | 中身を `ISingleValueObject.Reconstruct` に通す（**検証なし**） |
| `ExchangeValueObjectToPrimitive` | 値オブジェクトから中身の値を取り出す |

→ [optional.md](optional.md#変換連鎖する)

---

## 例外

すべて `TADAException` を継承する。

```
Exception
└─ TADAException
   ├─ DomainInvalidOperationException
   ├─ NestedTransactionException
   ├─ ObjectNotFoundException
   ├─ ObjectAlreadyExistException
   ├─ TransactionSessionNotFoundException
   └─ ValueObjectInvalidException
      ├─ ValueObjectNullException
      └─ ValueObjectLengthException
```

> `DomainInvalidOperationException` だけは `TADAException` を継承しつつ
> プライマリコンストラクタで宣言されている。捕捉の観点では他と同じ。

### `TADAException`

すべての基底。`(string? message = null, Exception? innerException = null)`。

### `ObjectNotFoundException`

**必要な対象が存在しなかった。**

```csharp
throw new ObjectNotFoundException(typeof(User), identifier);
```

`ObjectType` / `Identifier` プロパティを持つ。**情報付きのコンストラクタを推奨** —
呼び出し側でメッセージを組み立てなくてもログに型と識別子が出る。

**リポジトリは投げない。** 不在は `Optional<T>.Empty` で返り、
異常とみなすかは集約サービス / ユースケースが決める。

### `ObjectAlreadyExistException`

**存在してはいけない対象が存在した。**

```csharp
throw new ObjectAlreadyExistException(typeof(User), email);
```

`ObjectType` / `Identifier` プロパティを持つ。`Identifier` には主キーではなく、
一意でなければならない値（重複したメールアドレスなど）を渡すことが多い。

**リポジトリは投げない。** `SaveAsync` は upsert なので、既存レコードは失敗ではない。

> `Identifier` の `ToString()` が生成メッセージに入る。**秘密の値を渡さないこと。**

### `DomainInvalidOperationException`

ドメイン上許されない操作が行われた（利用停止中のユーザーを改名する、など）。

### `ValueObjectInvalidException`

値オブジェクトの不変条件に違反した。`Create` の中から投げる。

### `ValueObjectNullException`

`ValueObjectInvalidException` の派生。値が null だった。

### `ValueObjectLengthException`

`ValueObjectInvalidException` の派生。長さが範囲外だった。
`MinLength` / `MaxLength` / `CurrentLength` プロパティを持つ。

```csharp
throw new ValueObjectLengthException(
    minLength: MinLength, maxLength: MaxLength, currentLength: value.Length);
```

> **引数が 3 つとも `int`。** 順番を間違えてもコンパイルが通り、
> 間違った境界値がログに出る。順番は
> `minLength` → `maxLength` → `currentLength`（宣言された上下限が先、弾かれた長さが最後）。
> **名前付き引数で渡すこと。**

### `TransactionSessionNotFoundException`

開始されていないセッションを要求した。`SessionType` プロパティを持つ。

原因はほぼ常に、`ExecuteTransactionAsync<...>` の型引数にそのセッション型を
渡していないこと。

### `NestedTransactionException`

**実行中のトランザクションの中から、同じマネージャーでトランザクションを開始した。**

`ITransactionManager` は Scoped 登録なので、コマンドサービスが別のコマンドサービスを
呼ぶと同一インスタンスに行き着く。セッションはマネージャー単位で保持していて
「どの `ExecuteTransactionAsync` が開始したか」を区別しないため、内側の commit が
外側のセッションまで確定・`Dispose` してしまう。それを防ぐために**入れ子を禁止**している。

**直し方:** 共通処理をドメインサービス／集約サービスに切り出し、
同じ `ExecuteTransactionAsync` の本体からセッションを渡して呼ぶ。

→ [best-practices.md](best-practices.md#5-トランザクションを開始してよいのは-icommandservice-だけ)

→ [domain-model.md](domain-model.md#例外を投げる層)

---

## 関連

- [architecture.md](architecture.md) — 設計思想
- [getting-started.md](getting-started.md) — 手順
- [best-practices.md](best-practices.md) — 規約
- [domain-model.md](domain-model.md) — ドメインモデルの規約
- [use-case.md](use-case.md) — ユースケース層とトランザクション
- [optional.md](optional.md) — `Optional<T>` の三状態
- [migration.md](migration.md) — バージョン間の移行
