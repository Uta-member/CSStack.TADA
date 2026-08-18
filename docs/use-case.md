# ユースケース層とトランザクションの接続

`ICommandService` / `IQueryService` / ドメインサービスは、シグネチャだけ見ると
どれも `ExecuteAsync(req, ct)` で区別がつきません。**違いは型ではなく責務にあります。**
（ドメインサービスには TADA 由来の共通インターフェースが無く、`ExecuteAsync` を自分で
1 つ宣言しますが、シグネチャの形は同じです。）
このページでその境界と、TADA の名前の由来である「トランザクションとの接続点」を示します。

ドメイン層の規約は [domain-model.md](domain-model.md)、
`Optional<T>` は [optional.md](optional.md) を参照してください。

---

## 3 種のサービスの使い分け

| サービス | 一言で | トランザクション |
|---|---|---|
| `ICommandService` | 状態を変える 1 ユースケース。外部からの入口 | **ここで開始し、ここで終わる** |
| `IQueryService` | 読み取り専用の 1 ユースケース。DTO を返す | 原則として不要 |
| ドメインサービス | 集約をまたぐドメインのルール | 開始しない。渡されたセッションで動く |

> `ICommandService` / `IQueryService` と違い、ドメインサービスには TADA が用意する
> 共通インターフェースが無い。口の形（`ExecuteAsync` を 1 つ持つ）は規約であって、
> 型で強制されているわけではない（→ [domain-model.md](domain-model.md#ドメインサービス)）。

判断に迷ったら次の順で下ろしてください。

1. 1 つのエンティティで完結する → **エンティティのメソッド**
2. 1 つの集約で完結する → **集約サービス**
3. 集約をまたぐドメインのルール → **ドメインサービス**
4. 上記の呼び出し順序・認可・トランザクション → **コマンドサービス**
5. 画面や API に見せるための読み取り → **クエリサービス**

---

## コマンドサービスがトランザクションの境界

**TADA でトランザクションを知っている最も外側の層がコマンドサービスです。**
`ITransactionManager` をコンストラクターで受け取り、`ExecuteTransactionAsync` の中で処理を行い、
`TransactionSessions` から取り出したセッションを下の層へ渡します。

**これより下の層はトランザクションを開始も commit もロールバックもしません。**

```csharp
// ★ セッション型引数を持たない口。呼び出し側が見るのはこれだけ
//    リクエストはこの中にネストして Req と名付ける
public interface IChangeUserNameCommandService : ICommandService<IChangeUserNameCommandService.Req>
{
	sealed record Req(
		Guid UserId,
		string NewName,
		OperateInfo OperateInfo) : ICommandServiceDTO;
}

public sealed class ChangeUserNameCommandService<TUserSession> : IChangeUserNameCommandService
	where TUserSession : IDisposable
{
	private readonly ITransactionManager _transactionManager;

	// ★ 集約サービスも具象ではなくインターフェースで受ける
	private readonly IUserAggregateService<TUserSession> _userAggregateService;

	public ChangeUserNameCommandService(
		ITransactionManager transactionManager,
		IUserAggregateService<TUserSession> userAggregateService)
	{
		_transactionManager = transactionManager;
		_userAggregateService = userAggregateService;
	}

	public ValueTask ExecuteAsync(
		IChangeUserNameCommandService.Req req,
		CancellationToken cancellationToken = default)
	{
		// 値オブジェクトへの変換はここ。不正な入力なら Create が例外を投げる（型はプロジェクト側で定義する）
		var userId = UserId.Create(req.UserId);
		var newName = UserName.Create(req.NewName);

		return _transactionManager.ExecuteTransactionAsync<TUserSession>(
			async (sessions, token) =>
			{
				var session = sessions.GetSession<TUserSession>();
				await _userAggregateService.ChangeNameAsync(
					session,
					userId,
					newName,
					req.OperateInfo,
					token);
			},
			cancellationToken: cancellationToken);
	}
}
```

`ExecuteTransactionAsync` は begin → 本体 → commit を行い、
本体が例外を投げたらロールバックして再送出します。セッションはどの経路でも `Dispose` されます。

### セッション型は型引数で受け、名前に集約名を入れる

**ユースケースはセッションの具体型を知りません。** 上の例が
`ChangeUserNameCommandService<TUserSession>` になっているのはそのためで、
`MySession` のような具体型を書くとユースケース層が特定のデータストア実装に張り付きます。

**そして型引数の名前を `TSession` にしないでください。** ユースケースは複数の集約に触るのが普通で、
集約ごとにリポジトリが違えば書き込み先のストアも違いうる = セッション型も違います。
集約名を入れておけば、2 つ目の集約が加わったときにそのまま並べられます。

```csharp
public sealed class TransferCommandService<TAccountSession, TAuditLogSession>
	: ITransferCommandService
	where TAccountSession : IDisposable
	where TAuditLogSession : IDisposable
{
	// ExecuteTransactionAsync<TAccountSession, TAuditLogSession>(...) と書ける
}
```

**今 1 集約しか扱っていなくても `TUserSession` と書きます。** 後から増えたときに
`TSession` がどの集約のものだったか判別できず、改名が必要になるからです。

具体型が決まるのは DI 登録（下記）か、`new` でユースケースを組み立てる場所
— いずれもプレゼンテーション層です
（→ [architecture.md](architecture.md#ドメイン層に具体的なセッション型を書かない)）。

### ユースケースにはセッション型引数を持たない口を立てる

型引数をそのまま呼び出し側に見せると、コントローラーが
`ChangeUserNameCommandService<AppSession>` と書くことになり、
プレゼンテーション層にセッション型が漏れます。**そこでユースケースごとに
セッション型引数を持たないインターフェースを宣言し、実装がそれを実装します。**

```csharp
public interface IChangeUserNameCommandService : ICommandService<IChangeUserNameCommandService.Req>
{
	sealed record Req(Guid UserId, string NewName, OperateInfo OperateInfo) : ICommandServiceDTO;
}

public sealed class ChangeUserNameCommandService<TUserSession> : IChangeUserNameCommandService
	where TUserSession : IDisposable
{
	// ...
}
```

```csharp
// プレゼンテーション層。セッション型がどこにも出てこない
var commandService = scope.ServiceProvider.GetRequiredService<IChangeUserNameCommandService>();
await commandService.ExecuteAsync(new IChangeUserNameCommandService.Req(userId, "robert", operateInfo));
```

型引数を書くのは DI 登録の 1 行だけになります。テストのときも、
プレゼンテーション層はこの口を差し替えるだけで済みます。

**注入する下の層も具象クラスではなくインターフェースで受けてください。**

| 依存先 | 受ける型 |
|---|---|
| 集約サービス | `IUserAggregateService<TUserSession>`（`IAggregateService` を継承した口） |
| ドメインサービス | `IUserNameUniquenessService<TUserSession>`（自分で `ExecuteAsync` を宣言した口） |
| リポジトリ | 集約サービス越しに触るのが基本。直接なら `IUserRepository<TUserSession>` |

具象の集約サービスを注入すると、**このユースケースのテストが集約サービスの具象クラスと
リポジトリ実装を組み立てる話になります。** 口で受けていれば差し替えるだけで済みます
（→ [domain-model.md](domain-model.md#インターフェースを立ててから実装する)）。

### リクエストとレスポンスは口の中にネストする

`ICommandService` を継承した時点で「メソッドは 1 つ、リクエスト 1 型、レスポンス 1 型」が
確定しています。**つまり DTO は口と 1 対 1 に対応するので、口の中に `Req` / `Res` として
置いてください。** これは `IQueryService` でも、ドメインサービスの自作の口でも同じです。

```csharp
public interface ICreateUserCommandService
	: ICommandService<ICreateUserCommandService.Req, ICreateUserCommandService.Res>
{
	sealed record Req(string UserName, OperateInfo OperateInfo) : ICommandServiceDTO;

	sealed record Res(Guid UserId) : ICommandServiceDTO;
}
```

**なぜ:**

- 口から辿れる。`ICreateUserCommandService.Req` は必ずそこにあり、
  名前空間に平らに並んだ `~Req` 群から名前で探す必要がない
- 対応が固定される。外に置くと、別のユースケースのリクエストを渡しても
  型が合えばコンパイルが通ってしまう
- 名前が短くなる。ユースケースが増えても DTO 名の接頭辞が伸び続けない

レスポンスが要らないユースケースでは `Res` を作りません（`ICommandService<TReq>` を継承）。
複数のレスポンスで共有する読み取りモデル（一覧の 1 行など）は口と 1 対 1 ではないので、
ネストせず外に置きます。

**クエリサービスにも口を立ててください。** セッション型引数を持たないので理由は
コマンドサービスとは違い、「レスポンス型をそのクエリと 1 対 1 に固定し、
口から辿れる場所に置く」ためです。`IQueryService<UserListRes>` のままだと、
同じ形のレスポンスを返す別のクエリと DI 上で衝突します。

### 規約であって強制ではない

**基底クラスによる強制はしていません。** 2 つのトランザクションを順に張るユースケースや、
外部 API を呼ぶだけでトランザクションを必要としないユースケースも、
コマンドサービスとして書けるようにするためです。

ただし **`ITransactionManager` に一度も触れないコマンドサービスは、ほぼ確実に間違い**です。
書き込みがどこで確定するのか誰にも分からなくなります。

### 押さえておくべき 4 点

- **セッションの所有権は `ITransactionManager` にある。** commit / rollback / 例外の
  いずれの経路でも `ITransactionManager` が `Dispose` します。
  `ITransactionService<TSession>` の実装側で `Dispose` してはいけません
- **`TransactionManager` は Scoped で登録する。** スレッドセーフではなく、
  実行中のセッションを保持するため、Singleton にすると全リクエストで混線します
- **複数セッションの commit はアトミックではない。** 2 相コミットではないので、
  2 つ目が失敗しても 1 つ目は確定したまま残ります
- **トランザクションは入れ子にできない。** 実行中の `ExecuteTransactionAsync` の本体から
  同じマネージャーの `ExecuteTransactionAsync` を呼ぶと `NestedTransactionException` です。
  `ITransactionManager` は Scoped なので、**コマンドサービスが別のコマンドサービスを呼ぶと
  この状態**になります。共通処理はドメインサービス／集約サービスに切り出し、
  同じトランザクションの本体からセッションを渡して呼んでください
  （連続して 2 つのトランザクションを張るのは正当です）

### DI 登録

```csharp
// トランザクション。両方 Scoped
services.AddScoped<ITransactionManager, TransactionManager>();
services.AddScoped<ITransactionService<MySession>, MyTransactionService>();

// ドメイン。型引数を MySession に閉じるのはここ。すべて「インターフェース → 実装」
services.AddScoped<IUserRepository<MySession>, UserRepository>();
services.AddScoped<IUserAggregateService<MySession>, UserAggregateService<MySession>>();
services.AddScoped<IEmailUniquenessService<MySession>, EmailUniquenessService<MySession>>();

// ユースケース。TUserSession とリポジトリの TSession が一致することはこの行が保証している
services.AddScoped<IChangeUserNameCommandService, ChangeUserNameCommandService<MySession>>();
services.AddScoped<ISearchUsersQueryService, SearchUsersQueryService>();
```

**セッション型が確定するのはこの登録（プレゼンテーション層）だけです。**
ユースケースとリポジトリ実装を結びつける瞬間だからで、ドメイン層とユースケース層の
型定義には具体型が一度も現れません。

`TransactionManager` は `IServiceProvider` から `ITransactionService<TSession>` を解決します。
**セッション型ごとに `ITransactionService<TSession>` を登録していないと、
`BeginTransactionAsync` が `InvalidOperationException` を投げます。**

---

## クエリサービスはリポジトリを通さない

クエリサービスは **データストアを直接読み、呼び出し側の形に合わせた DTO を返します。**
リポジトリ・エンティティ・集約サービスを経由しません。

```csharp
public interface ISearchUsersQueryService
	: IQueryService<ISearchUsersQueryService.Req, ISearchUsersQueryService.Res>
{
	sealed record Req(string Keyword, int Skip, int Take) : IQueryServiceDTO;

	sealed record Res(IReadOnlyList<UserSummary> Items, int TotalCount) : IQueryServiceDTO;
}

// 複数のクエリで共有する読み取りモデルは、どれか 1 つの口の中に入れず外に置く
public sealed record UserSummary(Guid UserId, string Name, DateTimeOffset RegisteredAt);
```

これが `IRepository` に一覧取得や条件検索が無い理由です。書き込みモデルであるエンティティを
組み立ててから平坦化するのは無駄が多く、集約の境界を画面側へ引きずり出すことになります。

読み取りには通常トランザクションが要らないので、クエリサービスは
`ITransactionManager` ではなく自前の接続を持つのが普通です。
**実行中のトランザクションの未コミット状態を読む必要がある場合だけ**、
2 つ目のトランザクションを開始せず、リクエスト DTO でセッションを受け取ってください。

---

## ジェネリック型引数の罠

**`IQueryService` の 1 引数版だけ、型引数の意味が逆転しています。**

```csharp
ICommandService<TReq>          // 1 引数版は「リクエスト」
ICommandService<TReq, TRes>

IQueryService<TRes>            // 1 引数版は「レスポンス」← ここだけ逆
IQueryService<TReq, TRes>
```

引数を取らないクエリにも返すものはある、という理由でこうなっています。
つまり `IQueryService<Foo>` は「`Foo` を返す」、`ICommandService<Foo>` は「`Foo` を受け取る」です。
（ドメインサービスは継承する共通インターフェースが無いので、この罠自体がありません。
自分で宣言する `Req`/`ExecuteAsync` の形は好きに決められます。）

**リクエストがあるなら常に 2 引数版の `IQueryService<TReq, TRes>` を使ってください。**
そうすれば曖昧さは発生しません。

---

## DTO

2 つの DTO マーカー（`ICommandServiceDTO` / `IQueryServiceDTO`）はメンバーを持ちません。
**別の層の DTO を取り違えて渡せないようにするためだけの目印**です。
いずれも `record` で定義し、**それを使うサービスの口の中に `Req` / `Res` としてネスト**します
（→ [リクエストとレスポンスは口の中にネストする](#リクエストとレスポンスは口の中にネストする)）。

**ドメインサービスの `Req` にはマーカーがありません。** `IDomainServiceDTO` は共通インターフェースの
削除と合わせて v3.0.0 で削除されたため、ドメインサービスの `Req` はただの `record` です。
とはいえ「口の中にネストする」という置き場所の規約自体は同じです。

| DTO | 載せるもの | 載せないもの |
|---|---|---|
| `ICommandServiceDTO` | 呼び出し側から来た引数、操作情報 | エンティティ、セッション |
| `IQueryServiceDTO` | 検索条件、表示用の平坦なデータ | エンティティ |
| ドメインサービスの `Req`（マーカー無し） | 引数、**セッション**、エンティティ・値オブジェクト | — |

ドメインサービスの `Req` だけセッションを載せるのは、その `ExecuteAsync` に
セッション引数が無いためです。ドメインサービスはドメインの内側で動くので、
エンティティや値オブジェクトをそのまま渡して構いません。

**載せるのは具体型ではなく型引数です。** ネストしていれば口の型引数がそのまま使えます
（`IEmailUniquenessService<TUserSession>` の中の `record Req(TUserSession Session, ...)`）。
逆に `ICommandServiceDTO` / `IQueryServiceDTO` はセッションを持たないので、
**口も DTO も型引数を持ちません。** 境界の DTO に型引数が現れたら、セッションの存在が
呼び出し側へ漏れている兆候です。

---

## 早見表

| やりたいこと | 書き方 |
|---|---|
| 状態を変えるユースケース | `ICommandService`。`ITransactionManager.ExecuteTransactionAsync` で包む |
| 読み取りのユースケース | `IQueryService<TReq, TRes>`。ストアを直接読んで DTO を返す |
| 集約をまたぐルール | ドメインサービス。自分で `ExecuteAsync` を宣言し、セッションはリクエスト DTO で受け取る |
| リクエスト / レスポンスを置く | それを使う口の中にネストして `Req` / `Res` と名付ける |
| セッションを取り出す | `sessions.GetSession<TUserSession>()` |
| セッション型を受け取る | 型引数で受ける。名前は `TSession` ではなく `T[集約名]Session` |
| セッションの具体型を決める | ユースケース層では決めない。DI 登録（プレゼンテーション層） |
| プレゼンテーションから呼ぶ | セッション型引数を持たない口（`IChangeUserNameCommandService`）を解決する |
| 集約サービスを注入する | `IUserAggregateService<TUserSession>`。具象クラスを受けない |
| ドメインサービスを注入する | `IUserNameUniquenessService<TUserSession>`。これにも専用の口を立てる |
| 入力を値オブジェクトにする | コマンドサービスの入口で `Create` |
| トランザクション管理の登録 | `ITransactionManager` と `ITransactionService<TSession>` を **Scoped** で |

## 関連

- [domain-model.md](domain-model.md) — エンティティ / 値オブジェクト / リポジトリの規約
- [optional.md](optional.md) — `Optional<T>` の三状態と `return null` の罠
- `tests/CSStack.TADA.Tests/TransactionManagerTests.cs` — トランザクションの振る舞いを固定したテスト
