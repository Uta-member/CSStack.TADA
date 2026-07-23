# はじめかた

インストールから、ユーザーを 1 人登録して読み出すところまで。
7 ステップで、途中で動かなくなる箇所（DI 登録）を先に潰す構成にしてある。

完成品は [../samples/CSStack.TADA.Sample/](../samples/CSStack.TADA.Sample/) にあり、
`dotnet run --project samples/CSStack.TADA.Sample` で動く。
**なぜこの形になるのか**は [architecture.md](architecture.md) を参照。

---

## インストール

```
dotnet add package CSStack.TADA
```

`net8.0` / `net10.0` のマルチターゲット。依存パッケージは無い。

DI を使うので、まだ入っていなければ合わせて入れる
（ASP.NET Core / Generic Host を使っているなら不要）。

```
dotnet add package Microsoft.Extensions.DependencyInjection
```

```csharp
using CSStack.TADA;
```

namespace はフォルダ構成に関係なくフラットな `CSStack.TADA` の 1 つだけ。
`CSStack.TADA.Domain` のような namespace は存在しない。

---

## Step 1. セッション型を決める

**最初に決めるのはセッション。** TADA はこの型を全レイヤーに引き回すので、
ここが決まらないと他が書けない。

条件は `IDisposable` を実装していることだけ。実システムでは `DbContext` や
`DbConnection` + `DbTransaction` を包んだ型になる。

```csharp
public sealed class AppSession : IDisposable
{
    private readonly AppDatabase _database;
    private readonly Dictionary<Guid, UserRow?> _pending = new();  // 未コミットの変更

    public AppSession(AppDatabase database) => _database = database;

    public void Commit()   { /* _pending を _database へ反映 */ }
    public void Rollback() { _pending.Clear(); }
    public void Dispose()  { /* 後始末。呼ぶのは TransactionManager だけ */ }

    public UserRow? ReadById(Guid id) { /* _pending を優先して読む */ }
    public void StageSave(UserRow row) => _pending[row.Id] = row;
}
```

> セッション型はドメイン層からも見えることになる。これは `TSession` を明示的に
> 伝播すると決めたことの帰結で、事故ではない。プロジェクトを分けるときの置き場所は
> [architecture.md](architecture.md#セッション型がドメイン層から見えることについて) を参照。

---

## Step 2. 値オブジェクトを作る

`record` で実装する。**検証は `Create` の中だけに書き、`Reconstruct` では検証しない。**

```csharp
public sealed record UserName
    : ISingleValueObject<string, UserName>, ILengthDefinedSingleValueObject
{
    private UserName(string value) => Value = value;   // ★ private

    public static int MaxLength => 16;
    public static int MinLength => 1;

    public string Value { get; }

    // 外部からの入力用。検証はここに書く
    public static UserName Create(string value)
    {
        if (value is null)
        {
            throw new ValueObjectNullException($"{nameof(UserName)} に null は指定できません。");
        }
        if (value.Length < MinLength || value.Length > MaxLength)
        {
            // 引数が 3 つとも int。順番を間違えてもコンパイルは通るので名前付きで渡す
            throw new ValueObjectLengthException(
                minLength: MinLength, maxLength: MaxLength, currentLength: value.Length);
        }

        return new UserName(value);
    }

    // 永続化された値の復元用。検証しない
    public static UserName Reconstruct(string value) => new(value);
}
```

押さえるところ:

- **コンストラクタは private。** `Create` / `Reconstruct` だけを入口にすると
  「存在しているインスタンス = 検証を通ったインスタンス」になる
- **`Validate` メンバーは存在しない。** 外から呼べる検証があると、
  未検証の値オブジェクトが存在しうることになってしまうため、v2.0.0 で削除された
- **`Reconstruct` が検証しないのは意図的。** ルールを厳しくした後でも、
  古いルールで保存されたデータを読み戻せるようにするため
- `ILengthDefinedSingleValueObject` は長さを**公開する**だけで強制はしない。
  強制するのは `Create`。画面側が `UserName.MaxLength` をそのまま使えるので、
  同じ数字を 2 箇所に書かずに済む

→ [domain-model.md](domain-model.md#値オブジェクト)

---

## Step 3. エンティティを作る

`EntityBase<TSelf, TIdentifier>` を継承する。`TSelf` には自分自身を渡す。

```csharp
public sealed class User : EntityBase<User, UserId>
{
    private User(UserId identifier, UserName name) => (Identifier, Name) = (identifier, name);

    public override UserId Identifier { get; }

    public UserName Name { get; private set; }

    public static User Create(UserId identifier, UserName name) => new(identifier, name);

    public static User Reconstruct(UserId identifier, UserName name) => new(identifier, name);

    // 1 エンティティで完結するルールはここに置く
    public void Rename(UserName name) => Name = name;
}
```

等価性は `EntityBase` が実装済みで、**「実行時型が同じ、かつ `Identifier` が等しい」**。
他のプロパティは見ないので、名前を変えても同じユーザーのまま。

→ [domain-model.md](domain-model.md#エンティティ)

---

## Step 4. リポジトリを作る

インターフェースはドメイン層、実装はインフラ層。

```csharp
public interface IUserRepository : IRepositoryDeletable<User, UserId, OperateInfo, AppSession>;
```

型引数は 4 つ: エンティティ / 識別子 / 操作情報 / セッション。
`TOperateInfo` は書き込みと一緒に記録する「誰が・いつ」で、読み取り系は受け取らない。

```csharp
public sealed class InMemoryUserRepository : IUserRepository
{
    public ValueTask<Optional<User>> FindByIdentifierAsync(
        AppSession session, UserId identifier, CancellationToken cancellationToken = default)
    {
        var row = session.ReadById(identifier.Value);

        // ★ 見つからないときは Empty。`return null;` と書かない
        if (row is null)
        {
            return ValueTask.FromResult(Optional<User>.Empty);
        }

        // 永続化された値からの復元なので Reconstruct
        var user = User.Reconstruct(UserId.Reconstruct(row.Id), UserName.Reconstruct(row.Name));
        return ValueTask.FromResult(Optional<User>.Some(user));
    }

    public ValueTask SaveAsync(
        AppSession session, User entity, OperateInfo operateInfo,
        CancellationToken cancellationToken = default)
    {
        session.StageSave(new UserRow(entity.Identifier.Value, entity.Name.Value, operateInfo));
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteAsync(/* ... */) { /* ... */ }
}
```

ここで踏みやすい地雷が 3 つ:

1. **`return null;` と書かない。** 暗黙変換で `Some(null)`（`HasValue = true`）になり、
   呼び出し側の `TryGetValue` が true を返したうえで `NullReferenceException` になる。
   不在は `Optional<T>.Empty`
2. **`ObjectNotFoundException` を投げない。** 不在は正常な結果。
   異常かどうかを決めるのは集約サービスかユースケース
3. **セッションをフィールドに持たない。** 引数で受け取るだけ。
   begin / commit / rollback / dispose のどれも行わない

`SaveAsync` は **upsert**（あれば更新、なければ挿入）なので、
「既に居る / 居ない」で失敗させない。一覧・条件検索のメソッドも足さない（Step 7）。

→ [domain-model.md](domain-model.md#リポジトリ)

---

## Step 5. トランザクションサービスを作る

セッション型ごとに 1 つ。`ITransactionManager` から呼ばれる。

```csharp
public sealed class AppTransactionService : ITransactionService<AppSession>
{
    private readonly AppDatabase _database;

    public AppTransactionService(AppDatabase database) => _database = database;

    public ValueTask<AppSession> BeginAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new AppSession(_database));

    public ValueTask CommitAsync(AppSession session, CancellationToken cancellationToken = default)
    {
        session.Commit();
        return ValueTask.CompletedTask;   // ★ Dispose しない
    }

    public ValueTask RollbackAsync(AppSession session, CancellationToken cancellationToken = default)
    {
        session.Rollback();
        return ValueTask.CompletedTask;   // ★ Dispose しない
    }
}
```

**セッションを `Dispose` してはいけない。** 所有権は `ITransactionManager` にあり、
commit 後・rollback 後・例外時のいずれの経路でもマネージャーが `Dispose` する。
ここで呼ぶと二重解放になる。

非ジェネリックの `ITransactionService` は実装しなくてよい。
`ITransactionService<TSession>` が明示的実装を提供している。

---

## Step 6. DI を登録する

**ここが最初につまずくところ。** 登録を間違えると、コンパイルは通って実行時に落ちる。

```csharp
var services = new ServiceCollection();

services.AddSingleton<AppDatabase>();   // データベース相当

// ---- TADA ----------------------------------------------------------------
// ★ TransactionManager は必ず Scoped
services.AddScoped<ITransactionManager, TransactionManager>();

// ★ セッション型ごとに ITransactionService<TSession> を登録する
services.AddScoped<ITransactionService<AppSession>, AppTransactionService>();

// ---- ドメイン層 ------------------------------------------------------------
services.AddScoped<IUserRepository, InMemoryUserRepository>();
services.AddScoped<UserAggregateService>();

// ---- ユースケース層 --------------------------------------------------------
services.AddScoped<ICommandService<CreateUserReq, CreateUserRes>, CreateUserCommandService>();
services.AddScoped<IQueryService<ListUsersRes>, ListUsersQueryService>();

var provider = services.BuildServiceProvider();
```

### 落とすと必ず落ちる 2 つ

**1. `ITransactionService<TSession>` を登録し忘れる**

`TransactionManager` はセッション型から `ITransactionService<TSession>` を
`IServiceProvider` 経由で解決するので、登録が無いとこうなる:

```
InvalidOperationException:
  No transaction service is registered for the session type 'MyApp.AppSession'.
  Register ITransactionService<AppSession> with the service provider.
```

**セッション型が複数あるなら、その数だけ登録する。**

**2. `TransactionManager` を Singleton で登録する**

```csharp
services.AddSingleton<ITransactionManager, TransactionManager>();  // ← 事故
```

`TransactionManager` は実行中のセッションを可変フィールドに保持し、
**スレッドセーフではない**。Singleton にすると全リクエストで同じインスタンスを共有し、
セッションが混線する。テストでは同時実行しないので気づかず、本番の負荷時にだけ壊れる。

同じ理由で、1 つのインスタンスを複数スレッドや並行 `Task` から同時に動かしてもいけない。

### 1 リクエスト = 1 スコープ

`Scoped` なので、ユースケースごとにスコープを作る。

```csharp
using var scope = provider.CreateScope();
var commandService = scope.ServiceProvider
    .GetRequiredService<ICommandService<CreateUserReq, CreateUserRes>>();
await commandService.ExecuteAsync(req);
```

ASP.NET Core ならリクエストごとにフレームワークがスコープを作るので、
`CreateScope` を自分で書く必要はない。コンソールアプリやバッチでは明示的に作る。

---

## Step 7. ユースケースを作る

### コマンドサービス（書き込み）

**ここがトランザクションの境界。** `ITransactionManager` を注入するのはこの層だけ。

```csharp
public sealed record CreateUserReq(string UserName, OperateInfo OperateInfo) : ICommandServiceDTO;
public sealed record CreateUserRes(Guid UserId) : ICommandServiceDTO;

public sealed class CreateUserCommandService : ICommandService<CreateUserReq, CreateUserRes>
{
    private readonly ITransactionManager _transactionManager;
    private readonly UserAggregateService _userAggregateService;

    public CreateUserCommandService(
        ITransactionManager transactionManager, UserAggregateService userAggregateService)
    {
        _transactionManager = transactionManager;
        _userAggregateService = userAggregateService;
    }

    public async ValueTask<CreateUserRes> ExecuteAsync(
        CreateUserReq req, CancellationToken cancellationToken = default)
    {
        var userId = UserId.New();

        await _transactionManager.ExecuteTransactionAsync<AppSession>(
            async (sessions, token) =>
            {
                var session = sessions.GetSession<AppSession>();

                // 外部入力を値オブジェクトへ。不正なら ValueObjectInvalidException 系が飛ぶ
                var userName = UserName.Create(req.UserName);

                var user = User.Create(userId, userName);
                await _userAggregateService.RegisterAsync(session, user, req.OperateInfo, token);
            },
            cancellationToken: cancellationToken);

        return new CreateUserRes(userId.Value);
    }
}
```

`ExecuteTransactionAsync` の中身は「全部成功するか、全部無かったことになるか」の単位。
本体が例外を投げれば自動でロールバックされ、最後まで通ればコミットされる。
本体の中で commit / rollback / dispose を呼んではいけない。

**レスポンスにエンティティを入れない。** エンティティは読み出したトランザクションのもので、
呼び出し側に届く頃にはセッションは `Dispose` 済み。DTO に詰め替える。

### クエリサービス（読み込み）

**リポジトリを通さず、ストアを直接読む。**

```csharp
public sealed record ListUsersRes(IReadOnlyList<UserSummary> Users) : IQueryServiceDTO;

public sealed class ListUsersQueryService : IQueryService<ListUsersRes>
{
    private readonly AppDatabase _database;

    public ListUsersQueryService(AppDatabase database) => _database = database;

    public ValueTask<ListUsersRes> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var users = _database.Users
            .Select(row => new UserSummary(row.Id, row.Name))
            .ToList();

        return ValueTask.FromResult(new ListUsersRes(users));
    }
}
```

読み取りにトランザクションは要らないので、`ITransactionManager` も注入しない。
実行中のトランザクションの未コミット状態を読む必要があるときだけ、
リクエスト DTO にセッションを載せて渡す（2 つ目のトランザクションを開始しない）。

> **型引数が 1 つの `IQueryService<TRes>` は「レスポンス」を表す。**
> `ICommandService<TReq>` の 1 つ目がリクエストなのと逆。
> 引数があるときは迷わず `IQueryService<TReq, TRes>` を使えばよい。

→ [use-case.md](use-case.md)

---

## 動かす

```csharp
using var scope = provider.CreateScope();

var createUser = scope.ServiceProvider
    .GetRequiredService<ICommandService<CreateUserReq, CreateUserRes>>();
var response = await createUser.ExecuteAsync(
    new CreateUserReq("alice", OperateInfo.Now("operator-1")));

Console.WriteLine(response.UserId);
```

---

## つまずいたら

| 症状 | 原因 |
|---|---|
| `InvalidOperationException: No transaction service is registered...` | `ITransactionService<TSession>` の登録漏れ（Step 6） |
| `TransactionSessionNotFoundException` | `ExecuteTransactionAsync<T>` に渡していないセッション型を `GetSession` した |
| 負荷時だけセッションが混ざる | `TransactionManager` を Singleton で登録している（Step 6） |
| `TryGetValue` が true なのに `NullReferenceException` | リポジトリで `return null;` と書いた → `Optional<T>.Empty` にする |
| セッションが二重に `Dispose` される | `ITransactionService` の実装側で `Dispose` を呼んでいる（Step 5） |
| コミットしたのにデータが無い | `ExecuteTransactionAsync` の外で `SaveAsync` を呼んでいる |
| 2 つ目のストアだけロールバックされない | 複数セッションの commit はアトミックではない（仕様） |

## 次に読むもの

- [architecture.md](architecture.md) — **なぜ `TSession` を引き回すのか**
- [best-practices.md](best-practices.md) — 規約の一覧
- [domain-model.md](domain-model.md) — エンティティ / 値オブジェクト / リポジトリ
- [use-case.md](use-case.md) — 3 種のサービスの使い分け
- [optional.md](optional.md) — `Optional<T>` の三状態
- [../samples/](../samples/) — 動く完成品
