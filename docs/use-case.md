# ユースケース層とトランザクションの接続

`ICommandService` / `IQueryService` / `IDomainService` は、シグネチャだけ見ると
どれも `ExecuteAsync(req, ct)` で区別がつきません。**違いは型ではなく責務にあります。**
このページでその境界と、TADA の名前の由来である「トランザクションとの接続点」を示します。

ドメイン層の規約は [domain-model.md](domain-model.md)、
`Optional<T>` は [optional.md](optional.md) を参照してください。

---

## 3 種のサービスの使い分け

| サービス | 一言で | トランザクション |
|---|---|---|
| `ICommandService` | 状態を変える 1 ユースケース。外部からの入口 | **ここで開始し、ここで終わる** |
| `IQueryService` | 読み取り専用の 1 ユースケース。DTO を返す | 原則として不要 |
| `IDomainService` | 集約をまたぐドメインのルール | 開始しない。渡されたセッションで動く |

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
public sealed record ChangeUserNameRequest(
	Guid UserId,
	string NewName,
	OperateInfo OperateInfo) : ICommandServiceDTO;

public sealed class ChangeUserNameCommandService : ICommandService<ChangeUserNameRequest>
{
	private readonly ITransactionManager _transactionManager;
	private readonly UserAggregateService _userAggregateService;

	public ChangeUserNameCommandService(
		ITransactionManager transactionManager,
		UserAggregateService userAggregateService)
	{
		_transactionManager = transactionManager;
		_userAggregateService = userAggregateService;
	}

	public ValueTask ExecuteAsync(
		ChangeUserNameRequest req,
		CancellationToken cancellationToken = default)
	{
		// 値オブジェクトへの変換はここ。不正な入力は ValueObjectInvalidException になる
		var userId = UserId.Create(req.UserId);
		var newName = UserName.Create(req.NewName);

		return _transactionManager.ExecuteTransactionAsync<MySession>(
			async (sessions, token) =>
			{
				var session = sessions.GetSession<MySession>();
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

// ドメイン
services.AddScoped<IUserRepository, UserRepository>();
services.AddScoped<UserAggregateService>();

// ユースケース
services.AddScoped<ICommandService<ChangeUserNameRequest>, ChangeUserNameCommandService>();
services.AddScoped<IQueryService<SearchUsersRequest, SearchUsersResponse>, SearchUsersQueryService>();
```

`TransactionManager` は `IServiceProvider` から `ITransactionService<TSession>` を解決します。
**セッション型ごとに `ITransactionService<TSession>` を登録していないと、
`BeginTransactionAsync` が `InvalidOperationException` を投げます。**

---

## クエリサービスはリポジトリを通さない

クエリサービスは **データストアを直接読み、呼び出し側の形に合わせた DTO を返します。**
リポジトリ・エンティティ・集約サービスを経由しません。

```csharp
public sealed record SearchUsersRequest(string Keyword, int Skip, int Take) : IQueryServiceDTO;

public sealed record SearchUsersResponse(
	IReadOnlyList<SearchUsersResponse.Item> Items,
	int TotalCount) : IQueryServiceDTO
{
	public sealed record Item(Guid UserId, string Name, DateTimeOffset RegisteredAt);
}
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

IDomainService<TReq>           // 1 引数版は「リクエスト」
IDomainService<TReq, TRes>

IQueryService<TRes>            // 1 引数版は「レスポンス」← ここだけ逆
IQueryService<TReq, TRes>
```

引数を取らないクエリにも返すものはある、という理由でこうなっています。
つまり `IQueryService<Foo>` は「`Foo` を返す」、`ICommandService<Foo>` は「`Foo` を受け取る」です。

**リクエストがあるなら常に 2 引数版の `IQueryService<TReq, TRes>` を使ってください。**
そうすれば曖昧さは発生しません。

---

## DTO

3 つの DTO マーカー（`ICommandServiceDTO` / `IQueryServiceDTO` / `IDomainServiceDTO`）は
メンバーを持ちません。**別の層の DTO を取り違えて渡せないようにするためだけの目印**です。
いずれも `record` で定義してください。

| DTO | 載せるもの | 載せないもの |
|---|---|---|
| `ICommandServiceDTO` | 呼び出し側から来た引数、操作情報 | エンティティ、セッション |
| `IQueryServiceDTO` | 検索条件、表示用の平坦なデータ | エンティティ |
| `IDomainServiceDTO` | 引数、**セッション**、エンティティ・値オブジェクト | — |

`IDomainServiceDTO` だけセッションを載せるのは、`IDomainService.ExecuteAsync` に
セッション引数が無いためです。ドメインサービスはドメインの内側で動くので、
エンティティや値オブジェクトをそのまま渡して構いません。

---

## 早見表

| やりたいこと | 書き方 |
|---|---|
| 状態を変えるユースケース | `ICommandService`。`ITransactionManager.ExecuteTransactionAsync` で包む |
| 読み取りのユースケース | `IQueryService<TReq, TRes>`。ストアを直接読んで DTO を返す |
| 集約をまたぐルール | `IDomainService`。セッションはリクエスト DTO で受け取る |
| セッションを取り出す | `sessions.GetSession<MySession>()` |
| 入力を値オブジェクトにする | コマンドサービスの入口で `Create` |
| トランザクション管理の登録 | `ITransactionManager` と `ITransactionService<TSession>` を **Scoped** で |

## 関連

- [domain-model.md](domain-model.md) — エンティティ / 値オブジェクト / リポジトリの規約
- [optional.md](optional.md) — `Optional<T>` の三状態と `return null` の罠
- `tests/CSStack.TADA.Tests/TransactionManagerTests.cs` — トランザクションの振る舞いを固定したテスト
