# TADA の設計思想

Transaction-Aware Domain Architecture（TADA）が何を狙っていて、
DDD やクリーンアーキテクチャと何が違うのかをまとめる。

「なぜこう書かなければいけないのか」が分からないまま書くと、一般的な DDD の常識で
穴を埋めることになり、TADA の意図から外れたコードになる。このファイルはその穴埋めを防ぐためにある。

- 具体的な手順 → [getting-started.md](getting-started.md)
- 守るべき規約の一覧 → [best-practices.md](best-practices.md)
- 動くコード → [../samples/](../samples/)

---

## 一言でいうと

**トランザクションの範囲を、暗黙の文脈ではなく型として引数に持たせるアーキテクチャ。**

`Transaction-Aware` は「トランザクションを意識している」ではなく
「トランザクションがシグネチャに現れている」という意味だと思ってよい。

```csharp
// このメソッドは、誰かが既に始めたトランザクションの中で動く。
// 引数を見ればそれが分かる ← これが TADA の主張のほぼすべて
ValueTask<Optional<User>> FindByIdentifierAsync(
    AppSession session,
    UserId identifier,
    CancellationToken cancellationToken = default);
```

---

## なぜ `TSession` を全レイヤーに引き回すのか

TADA でいちばん目につくのがこれで、いちばん誤解されるのもこれ。
**冗長さと引き換えに何を買っているのか**を先に書く。

### トランザクション範囲の表し方は 3 通りある

| 方式 | 代表例 | トランザクション中かどうかの判別 |
|---|---|---|
| 暗黙の文脈 | `TransactionScope` / `AsyncLocal` | **できない**（実行時にしか分からない） |
| リポジトリが握る | リポジトリが `DbContext` をフィールドに持つ | **できない**（DI のライフタイム次第） |
| **明示的な引数** | **TADA** | **できる**（シグネチャに出ている） |

TADA は 3 番目を選ぶ。他の 2 つで起きることを順に見る。

### 暗黙の文脈（`TransactionScope` など）の問題

```csharp
// これはトランザクションの中で動いているのか？
await _userRepository.SaveAsync(user);
```

**このコードだけでは分からない。** 呼び出し元をすべて遡らないと判断できず、
「新しい呼び出し元が増えた」だけで前提が崩れる。しかも壊れ方が最悪で、
**コンパイルは通り、テストも通り、本番の同時実行時だけ壊れる。**

さらに `TransactionScope` は、複数の接続が絡むと黙って分散トランザクションに昇格し、
性能特性が変わる。「何もしていないのに遅くなった」の典型的な原因になる。

### リポジトリがセッションを握る方式の問題

```csharp
public class UserRepository
{
    private readonly AppDbContext _context;  // DI で注入され、リクエストスコープで共有される
}
```

一見きれいだが、こうなる:

- **トランザクションの寿命 = DI スコープの寿命**になる。1 リクエスト中に
  「先にログだけ確定させて、本処理は別トランザクション」ができない
- 2 つのリポジトリが同じトランザクションに参加するかどうかが、DI の登録次第で変わる。
  `AddScoped` を `AddTransient` に変えただけで暗黙に壊れる
- トランザクションの境界がフレームワーク（ASP.NET Core のリクエストスコープ）に依存し、
  バッチやコンソールアプリに持っていくと動かない

### TADA の選択

セッションを**引数**にすると、上のすべてが型の話になる。

```csharp
// session を受け取る = 呼び出し元のトランザクションの中で動く
await _repository.SaveAsync(session, user, operateInfo, token);
```

得られるもの:

1. **シグネチャが嘘をつかない。** `TSession` を受け取るメソッドは、必ず誰かのトランザクションの中に居る。
   受け取らないメソッドは居ない。読めば分かる
2. **境界が grep できる。** `ExecuteTransactionAsync` を検索すれば、トランザクションを開始している箇所が
   すべて出る。暗黙の文脈ではこれができない
3. **型が違えばストアも違う。** `AppSession` と `AuditLogSession` は別の型なので、
   取り違えるとコンパイルエラーになる。マネージャーが
   `ITransactionService<TSession>` を解決できるのも型で区別しているから
4. **DI のライフタイムに依存しない。** リポジトリはセッションをフィールドに持たないので、
   Transient でも Scoped でも挙動が変わらない
5. **テストが素直。** セッションを手で作って渡すだけでよく、DI コンテナも
   `TransactionScope` の仕掛けも要らない

### 代償

**正直に書くと、冗長。** すべてのメソッドに引数が 1 つ増え、
ドメイン層のインターフェースにセッション型が型引数として現れる。

TADA はこれを「隠すべきコスト」ではなく「払う価値のある明示化」と見なす。
トランザクションの範囲は業務の正しさに直結するので、暗黙にしてよい種類の詳細ではない、
という判断がこのアーキテクチャの前提にある。

**この前提に納得できないなら TADA は合わない。** 納得できるなら、
以降の「なぜこんな型引数の数なのか」はすべてこの 1 点から導かれる。

---

## レイヤー構成

```
┌─────────────────────────────────────────────────────────────┐
│ Presentation        Controller / Minimal API / CLI          │
│                     ICommandService と IQueryService を呼ぶ  │
│                     トランザクションのことは知らない         │
└───────────────────────────┬─────────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────────┐
│ UseCase             ICommandService  ← ★トランザクションの境界│
│                     IQueryService                            │
│                     ITransactionManager を注入して            │
│                     ExecuteTransactionAsync で包む            │
└───────────────────────────┬─────────────────────────────────┘
                            │  session を引数で渡す
┌───────────────────────────▼─────────────────────────────────┐
│ Domain              Entity / ValueObject                     │
│                     IAggregateService / IDomainService       │
│                     IRepository（インターフェースのみ）       │
│                     トランザクションを開始しない              │
└───────────────────────────┬─────────────────────────────────┘
                            │  実装する
┌───────────────────────────▼─────────────────────────────────┐
│ Infrastructure      IRepository の実装                        │
│                     ITransactionService<TSession> の実装      │
│                     セッション型（DbContext など）            │
│                     IQueryService の実装（ストアを直接読む）  │
└─────────────────────────────────────────────────────────────┘
```

依存の向きは Presentation → UseCase → Domain で、Infrastructure は
Domain のインターフェースを実装する側から刺さる（依存性逆転）。ここは
クリーンアーキテクチャと同じ。

### `src/` のフォルダとの対応

| フォルダ | 中身 | 実装する層 |
|---|---|---|
| `Domain/Entity` `Domain/ValueObject` | `EntityBase` `IValueObject` など | Domain |
| `Domain/Repository` | `IRepository` `IRepositoryDeletable` | 宣言は Domain / 実装は Infrastructure |
| `Domain/AggregateService` `Domain/DomainService` | 集約サービス・ドメインサービス | Domain |
| `UseCase/CommandService` `UseCase/QueryService` | ユースケースの入口 | UseCase（クエリの実装は Infrastructure 寄り） |
| `UseCase/TransactionService` | `ITransactionManager` `ITransactionService` | 宣言は UseCase / 実装は Infrastructure |

> namespace はフォルダに関係なく `CSStack.TADA` のフラット。
> 利用者が `using CSStack.TADA;` 1 行で済むようにするための意図的な設計。

---

## セッション伝播モデル

登録から実行まで、セッションがどこで生まれてどこで消えるか。

```
ICommandService.ExecuteAsync
  └─ ITransactionManager.ExecuteTransactionAsync<AppSession>(...)
       │
       ├─ 1. ITransactionService<AppSession> を IServiceProvider から解決
       │     （見つからないと InvalidOperationException）
       ├─ 2. BeginAsync() でセッションを生成
       │
       ├─ 3. 本体を実行 ─────────────────────────────┐
       │     sessions.GetSession<AppSession>()        │
       │       → 集約サービス(session, ...)           │ ここだけが
       │           → リポジトリ(session, ...)         │ 業務ロジック
       │       → ドメインサービス(DTO に session)     │
       │                                              │
       ├─ 4a. 正常終了 → CommitAsync() ──────────────┘
       │   4b. 例外    → RollbackAsync()（キャンセル済みでも実行される）
       │
       └─ 5. Dispose()  ← ★ マネージャーが必ず行う
```

### 押さえるべき 3 点

1. **セッションの所有権はマネージャーにある。**
   commit / rollback / 例外のどの経路でもマネージャーが `Dispose` する。
   `ITransactionService<TSession>` の実装側で `Dispose` してはいけない（二重解放になる）
2. **`TransactionManager` は Scoped で登録する。**
   実行中のセッションを可変フィールドに保持しスレッドセーフではないので、
   Singleton にすると全リクエストで混線する
3. **複数セッションの commit はアトミックではない。**
   2 相コミットではないので、2 つ目の commit が失敗しても 1 つ目は確定したまま残る。
   「確定してほしいストアは 1 つだけ」に設計するか、操作を冪等・再実行可能にする

---

## DDD / クリーンアーキテクチャとの差分

共通しているところは多い。**違うのは実質 4 点だけ**なので、そこだけ覚えればよい。

| | 一般的な DDD / クリーンアーキテクチャ | TADA |
|---|---|---|
| トランザクション範囲 | Unit of Work を注入 / 暗黙の文脈 | **`TSession` を引数で明示的に伝播** |
| リポジトリの検索系 | `FindAll` `FindBy...` を持つことが多い | **持たない。** 一覧・検索は `IQueryService` |
| 集約サービス | 任意。無いこともある | **1 集約 = エンティティ 1・リポジトリ 1・集約サービス 1** を型で表明 |
| 不在の表現 | `null` / 例外 / `Maybe` と揺れる | **`Optional<T>` に固定。** 例外に変えるのは上位層 |

### 1. トランザクション範囲

上で書いたとおり。ここが名前の由来でもある。

### 2. リポジトリに検索系メソッドを足さない

`IRepository` にあるのは `FindByIdentifierAsync` と `SaveAsync`（upsert）だけ
（削除するなら `IRepositoryDeletable` で `DeleteAsync` が加わる）。

一覧・条件検索・ページングは `IQueryService` の仕事で、**リポジトリを通さずストアを直接読む。**
エンティティは書き込みモデルであり、それを組み立ててから一覧用に潰すのは無駄な上に、
集約の境界を画面側へ引きずり出すことになる。CQRS の読み書き分離を、
インターフェースの形として強制している。

→ 詳細は [domain-model.md](domain-model.md) と [use-case.md](use-case.md)

### 3. 集約の単位を型で表明する

```csharp
IAggregateService<TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession>
```

型引数 5 個は削り忘れではない。**「この集約にはエンティティが 1 つ、
その上のリポジトリが 1 つ、エンティティに到達できるサービスが 1 つ」**
という TADA の集約の定義を、そのまま型で書いている。

### 4. 不在は `Optional<T>`

`FindByIdentifierAsync` は見つからなければ `Optional<T>.Empty` を返し、
`ObjectNotFoundException` は投げない。「見つからない」が異常かどうかは操作次第
（削除済みのものをもう一度削除するのは問題ないが、無い口座から引き落とすのは問題）で、
**それを知っているのは呼び出し側だけ**だから。

例外に変えるのは集約サービスかユースケース層。

→ 詳細は [optional.md](optional.md)

---

## セッション型がドメイン層から見えることについて

`IUserRepository : IRepositoryDeletable<User, UserId, OperateInfo, AppSession>` と書くと、
ドメイン層のインターフェースがインフラ層の型 `AppSession` を参照することになる。
**これは設計ミスではなく、`TSession` を明示的に伝播すると決めたことの必然的な帰結。**

プロジェクトを分けるときの選択肢は 2 つ。

**A. セッション型を共有プロジェクトに置く（推奨）**

```
MyApp.Abstractions/   ← AppSession（IDisposable を実装するだけの薄い型）
MyApp.Domain/         ← Abstractions を参照
MyApp.Infrastructure/ ← Abstractions を参照し、AppSession の中身を実装
```

セッション型を「トランザクションの識別子」程度の薄い型に保つのがコツ。
`DbContext` をそのままセッションにすると、ドメイン層から EF Core が見えてしまう。

**B. リポジトリインターフェースを `TSession` で開いておく**

```csharp
public interface IUserRepository<TSession> : IRepository<User, UserId, OperateInfo, TSession>
    where TSession : IDisposable;
```

ドメイン層は具体的なセッション型を知らずに済むが、型引数が上まで波及する。
アプリケーションが 1 つのセッション型しか使わないなら、A のほうが読みやすい。

サンプルは単一プロジェクトなので、この分割は行っていない。

---

## 早見表

| 疑問 | 答え |
|---|---|
| なぜ引数が多いのか | トランザクション範囲を暗黙にしないため |
| トランザクションを開始してよい層は | `ICommandService` だけ |
| セッションを `Dispose` するのは | `ITransactionManager`。実装側ではやらない |
| `TransactionManager` のライフタイムは | **Scoped**。Singleton は事故 |
| 一覧を取りたい | `IQueryService`。リポジトリには足さない |
| 見つからなかった | `Optional<T>.Empty`。例外にするのは上位層 |
| 複数ストアに書きたい | できるが**アトミックではない**。設計で避ける |
| ドメイン層がセッション型を参照してよいか | よい。分けたいなら共有プロジェクトへ |

## 関連

- [getting-started.md](getting-started.md) — ゼロから動かすまで
- [best-practices.md](best-practices.md) — 規約の一覧
- [domain-model.md](domain-model.md) — エンティティ / 値オブジェクト / リポジトリ
- [use-case.md](use-case.md) — 3 種のサービスの使い分けとトランザクションの境界
- [optional.md](optional.md) — `Optional<T>` の三状態
- [api-reference.md](api-reference.md) — 公開型の一覧
