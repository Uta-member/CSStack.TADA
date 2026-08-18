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
    TSession session,
    UserId identifier,
    CancellationToken cancellationToken = default);
```

引数にあるのは**セッション型の型引数**であって、インフラ層の具体型ではない。
ドメイン層に `AppSession` と書くのは推奨されない
→ [ドメイン層に具体的なセッション型を書かない](#ドメイン層に具体的なセッション型を書かない)

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
ドメイン層のインターフェースにセッション型の**型引数**が現れ、
それがユースケース層まで波及する。

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
│                     IAggregateService / ドメインサービスの口 │
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

**セッション型もこの向きに従う。** UseCase と Domain は型引数として受け取るだけで
具体型を知らず、Infrastructure が具体型を定義して実装で閉じ、Presentation が
DI 登録で両者を結びつける → [ドメイン層に具体的なセッション型を書かない](#ドメイン層に具体的なセッション型を書かない)

### `src/` のフォルダとの対応

| フォルダ | 中身 | 実装する層 |
|---|---|---|
| `Domain/Entity` `Domain/ValueObject` | `EntityBase` `IValueObject` など | Domain |
| `Domain/Repository` | `IRepository` `IRepositoryDeletable` | 宣言は Domain / 実装は Infrastructure |
| `Domain/AggregateService` | 集約サービス（`IAggregateService` / `AggregateServiceBase`） | Domain |
| `UseCase/CommandService` `UseCase/QueryService` | ユースケースの入口 | UseCase（クエリの実装は Infrastructure 寄り） |
| `UseCase/TransactionService` | `ITransactionManager` `ITransactionService` | 宣言は UseCase / 実装は Infrastructure |

> namespace はフォルダに関係なく `CSStack.TADA` のフラット。
> 利用者が `using CSStack.TADA;` 1 行で済むようにするための意図的な設計。

> **ドメインサービス用の共通インターフェースはライブラリに無い。** かつて `Domain/DomainService`
> にあった `IDomainService<TReq>` 系は削除されたので、ドメインサービスの口は利用側が自分で
> `ExecuteAsync` を 1 つ宣言する。「専用の口を立て、リクエストをその中にネストする」という
> 規約自体は他の 3 種のサービスと変わらない → [use-case.md](use-case.md#3-種のサービスの使い分け)。

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
見つからないことをそのまま例外にはしない。「見つからない」が異常かどうかは操作次第
（削除済みのものをもう一度削除するのは問題ないが、無い口座から引き落とすのは問題）で、
**それを知っているのは呼び出し側だけ**だから。

例外に変えるのは集約サービスかユースケース層。

→ 詳細は [optional.md](optional.md)

---

## ドメイン層に具体的なセッション型を書かない

これは書ける。**が、推奨されない。**

```csharp
// ✗ ドメイン層のインターフェースがインフラ層の実トランザクション因子を名指ししている
public interface IUserRepository : IRepositoryDeletable<User, UserId, OperateInfo, AppSession>;
```

コンパイルは通り、サンプル程度の規模なら動く。しかし**この 1 行で、TADA を採用する利点と
DDD の利点がほぼ消える。**

- **依存の向きが逆転する。** ドメイン層が特定のデータストア実装に張り付き、
  「インフラはドメインのインターフェースを実装する側から刺さる」という前提が崩れる
- **テストダブルが差し込めない。** ドメインの単体テストのために毎回 `AppSession` を
  用意することになる
- **集約ごとにストアが違う構成へ進めない。** 1 つの具体型に固定されているので、
  「ユーザーは RDB、監査ログは別ストア」という珍しくもない構成に手が届かない
- **セッションが太ると全部が漏れる。** `DbContext` をそのままセッションにした場合、
  ドメイン層から EF Core が見えてしまう

`TSession` を明示的に伝播するという判断は、**「セッション型を型引数として引き回す」ことを
意味していて、「具体型をドメインに焼き込む」ことは意味していない。**

### 正しい形: 型引数として外から受け取る

**ドメイン層とユースケース層は、セッション型を型引数として受け取るだけで具体型を知らない。**

```csharp
// ✓ Domain: 集約が扱うリポジトリは 1 つなので、名前は素の TSession でよい
public interface IUserRepository<TSession> : IRepositoryDeletable<User, UserId, OperateInfo, TSession>
    where TSession : IDisposable;

public sealed class UserAggregateService<TSession>
    : AggregateServiceBase<User, UserId, IUserRepository<TSession>, OperateInfo, TSession>
    where TSession : IDisposable
{
    public UserAggregateService(IUserRepository<TSession> repository) : base(repository) { }

    // 集約サービスのメソッドはドメインの操作の名前にする（SaveAsync のような汎用名にしない）
    public async ValueTask RenameAsync(
        TSession session, UserId id, UserName newName, OperateInfo operateInfo, CancellationToken ct = default)
    {
        var user = await GetRequiredAsync(session, id, ct);
        user.Rename(newName);
        await Repository.SaveAsync(session, user, operateInfo, ct);
    }
}
```

### 型引数の名前: 集約をまたぐ層では `T[集約名]Session`

集約サービスまでは扱うリポジトリが 1 つなので `TSession` で足りる。
**ユースケース層とドメインサービスでは `TSession` という名前を使わない。**

これらの層は複数の集約に触る可能性があり、**集約ごとにリポジトリが違えば、
書き込み先のデータストアも違いうる = セッション型も違う。**
そこで集約の名前を `T` と `Session` の間に入れて、並べられる名前にしておく。

```csharp
// ✓ UseCase: 集約ごとに別の型引数。ストアが違えば別の型が入る
public sealed class TransferCommandService<TAccountSession, TAuditLogSession>
    : ITransferCommandService
    where TAccountSession : IDisposable
    where TAuditLogSession : IDisposable
{
    public ValueTask ExecuteAsync(
        ITransferCommandService.Req req, CancellationToken cancellationToken = default)
        => _transactionManager.ExecuteTransactionAsync<TAccountSession, TAuditLogSession>(
            async (sessions, token) =>
            {
                var accountSession = sessions.GetSession<TAccountSession>();
                var auditSession = sessions.GetSession<TAuditLogSession>();
                // ...
            },
            cancellationToken: cancellationToken);
}
```

**今は 1 集約しか扱っていなくても `TUserSession` と書く。** 後から集約が増えたときに、
`TSession` がどの集約のセッションだったのか判別できなくなる。

> 複数セッションを 1 つのトランザクションで扱えることと、それが**アトミックに commit される**
> ことは別の話。2 相コミットではないので、確定してほしいストアは 1 つに設計する。

### 型が確定するのはプレゼンテーション層

では具体型はどこで決まるのか。**ユースケースとリポジトリを結びつける瞬間**、
つまりユースケースをインスタンス化するとき（引数にリポジトリを渡すとき）か、
DI コンテナに登録するときである。どちらもプレゼンテーション層。

```csharp
// Presentation: ここが唯一 AppSession という名前が出てくる場所（インフラ層の実装を除く）
services.AddScoped<ITransactionService<AppSession>, AppTransactionService>();

services.AddScoped<IUserRepository<AppSession>, InMemoryUserRepository>();
services.AddScoped<IUserAggregateService<AppSession>, UserAggregateService<AppSession>>();

services.AddScoped<ICreateUserCommandService, CreateUserCommandService<AppSession>>();
```

まとめると、セッション型に対する各層の関わり方はこうなる。

| 層 | セッション型 |
|---|---|
| Presentation | **具体型を決める**（DI 登録 / ユースケースの組み立て） |
| UseCase | 型引数 `T[集約名]Session` として受け取る |
| Domain | 型引数 `TSession`（集約をまたぐならこちらも `T[集約名]Session`）として受け取る |
| Infrastructure | **具体型を定義し、実装で閉じる**（`IUserRepository<AppSession>`） |

動く実例は [../samples/CSStack.TADA.Sample/](../samples/CSStack.TADA.Sample/) にある。

### 型引数を呼び出し側に見せない: 各層に口を立てる

型引数を上の層まで波及させると、最後に困るのは呼び出し側になる。
コントローラーが `CreateUserCommandService<AppSession>` と書く羽目になるなら、
プレゼンテーション層にセッション型が漏れているのと同じことになってしまう。

**そこで、集約サービスとユースケースにはインターフェースを立てて、そちらを注入・解決する。**

```csharp
// Domain: 集約サービスの口。IAggregateService を継承して集約の操作を宣言する
// ★ 並ぶのはドメインの操作だけ。SaveAsync のような汎用的な口は置かない
public interface IUserAggregateService<TSession>
    : IAggregateService<User, UserId, IUserRepository<TSession>, OperateInfo, TSession>
    where TSession : IDisposable
{
    ValueTask RegisterAsync(TSession s, User u, OperateInfo o, CancellationToken ct = default);
    ValueTask RenameAsync(
        TSession s, UserId id, UserName newName, OperateInfo o, CancellationToken ct = default);
}

// Domain: 実装。基底クラスは実装の詳細で、上の層はこのクラスを知らない
public sealed class UserAggregateService<TSession>
    : AggregateServiceBase<User, UserId, IUserRepository<TSession>, OperateInfo, TSession>,
    IUserAggregateService<TSession>
    where TSession : IDisposable { /* ... */ }

// UseCase: ユースケースの口。★ セッション型引数を持たない
// ★ リクエストとレスポンスはこの中にネストする
public interface ICreateUserCommandService
    : ICommandService<ICreateUserCommandService.Req, ICreateUserCommandService.Res>
{
    sealed record Req(string UserName, OperateInfo OperateInfo) : ICommandServiceDTO;

    sealed record Res(Guid UserId) : ICommandServiceDTO;
}

public sealed class CreateUserCommandService<TUserSession> : ICreateUserCommandService
    where TUserSession : IDisposable
{
    public CreateUserCommandService(
        ITransactionManager transactionManager,
        IUserAggregateService<TUserSession> userAggregateService) { /* ... */ }
}
```

こうすると呼び出し側はこうなる。

```csharp
// Presentation: セッション型がどこにも出てこない
var commandService = scope.ServiceProvider.GetRequiredService<ICreateUserCommandService>();
await commandService.ExecuteAsync(new ICreateUserCommandService.Req("alice", operateInfo));
```

得られるもの:

1. **プレゼンテーション層がセッション型を書かずに済む。** 型引数が現れるのは DI 登録の 1 行だけ
2. **ユースケースのテストで具象クラスを組み立てなくてよい。**
   `IUserAggregateService<TSession>` を差し替えるだけで済む。具象の集約サービスを注入していると、
   その先のリポジトリ実装まで用意する話になる
3. **プレゼンテーション層のテストでユースケースを差し替えられる**

**ドメインサービスとクエリサービスにも口を立てる。** これらはセッション型を呼び出し側に
見せないので理由は上の 3 点ではなく、**リクエスト / レスポンスの置き場所**にある。
ドメインサービスには TADA 由来の共通インターフェースが無いので、注入するだけなら
口を立てずに実装クラスをそのまま注入しても動く。それでも口を立てるのは、
DTO を口の中に `Req` / `Res` としてネストしておくと、口から必ず辿れて対応が 1 対 1 に固定される
からである（→ [use-case.md](use-case.md#リクエストとレスポンスは口の中にネストする)）。

### 割り切って具体型を書く場合

**単一プロジェクトの小さなアプリで、ストアが 1 つしかないと言い切れるなら**、
セッション型を薄いクラスとして共有プロジェクトに置き、ドメイン層から直接参照する構成もありうる。

```
MyApp.Abstractions/   ← AppSession（IDisposable を実装するだけの薄い型）
MyApp.Domain/         ← Abstractions を参照
MyApp.Infrastructure/ ← Abstractions を参照し、AppSession の中身を実装
```

型引数が上の層まで波及しないぶん読みやすい。ただし**上に挙げた利点を捨てる選択**であり、
後から型引数に開き直すのはドメイン層とユースケース層の全面改修になる。
迷ったら開いておくほうがよい。

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
| ドメイン層に `AppSession` と書いてよいか | **書かない。** 型引数で受け取る |
| セッション型の具体型が決まるのは | **プレゼンテーション層**（DI 登録）とインフラ層の実装 |
| ユースケースの型引数名は | `TSession` ではなく `TUserSession` のように集約名を入れる |
| 集約サービスをそのまま注入してよいか | **インターフェースを立てる。** `AggregateServiceBase` は実装の詳細 |
| ユースケースの呼び出しに型引数が要るか | **要らない。** セッション型引数を持たない口を立てる |

## 関連

- [getting-started.md](getting-started.md) — ゼロから動かすまで
- [best-practices.md](best-practices.md) — 規約の一覧
- [domain-model.md](domain-model.md) — エンティティ / 値オブジェクト / リポジトリ
- [use-case.md](use-case.md) — 3 種のサービスの使い分けとトランザクションの境界
- [optional.md](optional.md) — `Optional<T>` の三状態
- [api-reference.md](api-reference.md) — 公開型の一覧
