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
ここが決まらないと他が書けない。ただし引き回すのは**型引数**で、
この具体型を名指しするのはインフラ層とプレゼンテーション層だけ（Step 4・Step 6）。

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

> **この型の名前をドメイン層に書かないこと。** `AppSession` はインフラ層の型なので、
> ドメイン層・ユースケース層は型引数として受け取る（Step 4）。
> 理由と全体像は
> [architecture.md](architecture.md#ドメイン層に具体的なセッション型を書かない) を参照。

---

## Step 2. 値オブジェクトを作る

`record` で実装する。**検証は `Create` の中だけに書き、`Reconstruct` では検証しない。**

```csharp
public sealed record UserName : ISingleValueObject<string, UserName>
{
    private UserName(string value) => Value = value;   // ★ private

    // interface を介さない素の static メンバー。長さの上下限を公開するだけ
    public static int MaxLength => 16;
    public static int MinLength => 1;

    public string Value { get; }

    // 外部からの入力用。検証はここに書く
    public static UserName Create(string value)
    {
        CheckInvariants(value);
        return new UserName(value);
    }

    // 永続化された値の復元用。検証しない
    public static UserName Reconstruct(string value) => new(value);

    // Create と Reconstruct 後の再検証の両方から呼べるように、検証はヘルパーへ集約する
    public void Validate() => CheckInvariants(Value);

    private static void CheckInvariants(string value)
    {
        if (value is null)
        {
            throw new UserNameInvalidException($"{nameof(UserName)} に null は指定できません。");
        }
        if (value.Length < MinLength || value.Length > MaxLength)
        {
            // 引数が 3 つとも int。順番を間違えてもコンパイルは通るので名前付きで渡す
            throw new UserNameLengthException(
                minLength: MinLength, maxLength: MaxLength, currentLength: value.Length);
        }
    }
}
```

`UserNameInvalidException` / `UserNameLengthException` はこのプロジェクト自身が定義する例外。
TADA はもう値オブジェクト用の例外クラスを提供しないので、何を投げるかは利用側が決める
（→ [domain-model.md](domain-model.md#検証は-create-の中に書く)）。

押さえるところ:

- **コンストラクタは private。** `Create` / `Reconstruct` だけを入口にすると
  「存在しているインスタンス = 検証を通ったインスタンス」になる
- **`Validate()` は `Create` の代わりではない。** `IValueObject.Validate()` は必須メンバーだが、
  `Create` を通った時点で検証済みなので `Create` の中からは呼ばない。用意した理由は、
  あるインスタンスが `Create` を通ったかどうかを外部から検証する術が無いため。
  `Reconstruct` の後に使うのはその一例に過ぎず、`Validate()` 自体は何にも依存しない、
  今の値が現行の不変条件を満たしているかを確認するだけのプリミティブ
- **`Create` と `Validate` の検証は `CheckInvariants` のような `private` ヘルパーに集約する。**
  同じチェックを 2 か所に書かない
- **`Reconstruct` が検証しないのは意図的。** ルールを厳しくした後でも、
  古いルールで保存されたデータを読み戻せるようにするため
- 長さの上下限は `MaxLength` / `MinLength` を**素の static メンバーとして公開する**だけで強制はしない
  （`ILengthDefinedSingleValueObject` は v3.0.0 で削除された）。強制するのは `Create`。
  画面側が `UserName.MaxLength` をそのまま使えるので、同じ数字を 2 箇所に書かずに済む

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

    // IEntity<TIdentifier>.Validate() は必須メンバー。持っている値オブジェクトへ委譲すればよいことが多い
    public override void Validate()
    {
        Identifier.Validate();
        Name.Validate();
    }
}
```

等価性は `EntityBase` が実装済みで、**「実行時型が同じ、かつ `Identifier` が等しい」**。
他のプロパティは見ないので、名前を変えても同じユーザーのまま。

`EntityBase<TSelf, TIdentifier>` は `public abstract void Validate();` を宣言しているので、
派生クラスは必ず実装する。構築時の検証は `Create` の仕事のままで、`Validate()` はそれを置き換えない。

→ [domain-model.md](domain-model.md#エンティティ)

---

## Step 4. リポジトリを作る

インターフェースはドメイン層、実装はインフラ層。

```csharp
public interface IUserRepository<TSession> : IRepositoryDeletable<User, UserId, OperateInfo, TSession>
    where TSession : IDisposable;
```

型引数は 4 つ: エンティティ / 識別子 / 操作情報 / セッション。
`TOperateInfo` は書き込みと一緒に記録する「誰が・いつ」で、読み取り系は受け取らない。

**`AppSession` と書かずに `TSession` で開いてあることが重要。**
ここに Step 1 の具体型を書くと、ドメイン層がインフラ層に依存してしまう
（→ [architecture.md](architecture.md#ドメイン層に具体的なセッション型を書かない)）。
集約が扱うリポジトリは 1 つなので、名前は素の `TSession` でよい。

実装はインフラ層なので、ここで型引数を `AppSession` に閉じる。

```csharp
public sealed class InMemoryUserRepository : IUserRepository<AppSession>
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

ここで踏みやすい地雷が 4 つ:

1. **`return null;` と書かない。** 暗黙変換で `Some(null)`（`HasValue = true`）になり、
   呼び出し側の `TryGetValue` が true を返したうえで `NullReferenceException` になる。
   不在は `Optional<T>.Empty`
2. **見つからないことを例外にしない。** 不在は正常な結果。
   異常かどうかを決めるのは集約サービスかユースケース
3. **セッションをフィールドに持たない。** 引数で受け取るだけ。
   begin / commit / rollback / dispose のどれも行わない
4. **インターフェース側に `AppSession` と書かない。** 具体型を名指しするのは
   この実装クラスだけ。インターフェースは `TSession` のまま開いておく

`SaveAsync` は **upsert**（あれば更新、なければ挿入）なので、
「既に居る / 居ない」で失敗させない。一覧・条件検索のメソッドも足さない（Step 7）。

→ [domain-model.md](domain-model.md#リポジトリ)

### その上の集約サービス —— インターフェースを立ててから実装する

集約の操作（不在なら失敗させる、既に居たら失敗させる）は集約サービスに置く。
**まず `IAggregateService` を継承した口を宣言し、その実装として
`AggregateServiceBase` の派生クラスを書く。**

```csharp
// ★ 並べるのはドメインの操作だけ。SaveAsync のような汎用的な口は置かない
public interface IUserAggregateService<TSession>
    : IAggregateService<User, UserId, IUserRepository<TSession>, OperateInfo, TSession>
    where TSession : IDisposable
{
    ValueTask RegisterAsync(
        TSession session, User user, OperateInfo operateInfo, CancellationToken cancellationToken = default);

    ValueTask RenameAsync(
        TSession session, UserId identifier, UserName newName, OperateInfo operateInfo,
        CancellationToken cancellationToken = default);
}

public sealed class UserAggregateService<TSession>
    : AggregateServiceBase<User, UserId, IUserRepository<TSession>, OperateInfo, TSession>,
    IUserAggregateService<TSession>
    where TSession : IDisposable
{
    public UserAggregateService(IUserRepository<TSession> repository) : base(repository) { }

    public async ValueTask RegisterAsync(
        TSession session, User user, OperateInfo operateInfo, CancellationToken cancellationToken = default)
    {
        // SaveAsync は upsert なので、「既に居たら失敗」はここで先に読んで判断する
        var existing = await GetEntityByIdentifierAsync(session, user.Identifier, cancellationToken);
        if (existing.HasValue)
        {
            throw new UserAlreadyExistsException($"ユーザー '{user.Identifier}' は既に存在します。");
        }

        await Repository.SaveAsync(session, user, operateInfo, cancellationToken);
    }

    // 「取得する → 変更する → 保存する」を 1 つの操作に閉じる。
    // エンティティを上の層へ返さないので、保存忘れも集約の外での書き換えも起こらない
    public async ValueTask RenameAsync(
        TSession session, UserId identifier, UserName newName, OperateInfo operateInfo,
        CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredAsync(session, identifier, cancellationToken);
        user.Rename(newName);
        await Repository.SaveAsync(session, user, operateInfo, cancellationToken);
    }

    // 「居なければ失敗」を決めるのはこの層。リポジトリは Optional<User>.Empty を返すだけ。
    // ★ private に留める。口に載せるとエンティティが集約の外へ出てしまう
    private async ValueTask<User> GetRequiredAsync(
        TSession session, UserId identifier, CancellationToken cancellationToken)
    {
        var found = await GetEntityByIdentifierAsync(session, identifier, cancellationToken);
        if (!found.TryGetValue(out var user))
        {
            throw new UserNotFoundException(identifier.Value);
        }

        return user;
    }
}
```

`UserAlreadyExistsException` / `UserNotFoundException` はこのプロジェクトが自分で定義する例外。
TADA はもう `ObjectAlreadyExistException` / `ObjectNotFoundException` のような
汎用の例外クラスを提供しないので、ドメインの語彙で自前に定義する
（→ [domain-model.md](domain-model.md#例外を投げる層)）。

**具象クラスをユースケースに注入しないこと。** 基底クラスは実装の詳細で、
上の層に見せる契約はインターフェースのほう。具象を注入すると、
ユースケースのテストのために集約サービスとリポジトリ実装を組み立てることになる。

**この口には `SaveAsync` を置かないこと。** 何でも受け取る `SaveAsync` が `RegisterAsync` と
並んでいると、呼ぶ側はたいてい `SaveAsync` を選び、「既に居たら失敗」が素通りされる。

→ [domain-model.md](domain-model.md#インターフェースを立ててから実装する)

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
// ★ 型引数を AppSession に閉じるのはここ。ドメイン層の型定義には AppSession が現れない
// ★ 登録はすべて「インターフェース → 実装」。上の層は具象クラスを知らない
services.AddScoped<IUserRepository<AppSession>, InMemoryUserRepository>();
services.AddScoped<IUserAggregateService<AppSession>, UserAggregateService<AppSession>>();

// ---- ユースケース層 --------------------------------------------------------
// ★ ユースケースの TUserSession と集約サービスの TSession が一致することを保証しているのはこの行
services.AddScoped<ICreateUserCommandService, CreateUserCommandService<AppSession>>();
services.AddScoped<IListUsersQueryService, ListUsersQueryService>();

var provider = services.BuildServiceProvider();
```

**セッション型が確定するのはここ（プレゼンテーション層）だけ。**
ユースケースとリポジトリ実装を結びつける瞬間なので、型が決まるのも当然この場所になる。
`new` でユースケースを組み立てる場合も同じで、コンストラクタ引数にリポジトリを渡す時点で決まる
（→ [architecture.md](architecture.md#型が確定するのはプレゼンテーション層)）。

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
var commandService = scope.ServiceProvider.GetRequiredService<ICreateUserCommandService>();
await commandService.ExecuteAsync(req);
```

ASP.NET Core ならリクエストごとにフレームワークがスコープを作るので、
`CreateScope` を自分で書く必要はない。コンソールアプリやバッチでは明示的に作る。

ここに `AppSession` が出てこないのは、`ICreateUserCommandService`（Step 7）が
セッション型引数を持たない口だから。型引数を書くのは上の登録の 1 行だけ。

---

## Step 7. ユースケースを作る

### コマンドサービス（書き込み）

**ここがトランザクションの境界。** `ITransactionManager` を注入するのはこの層だけ。

```csharp
// ★ セッション型引数を持たない口を立てる。呼び出し側が見るのはこれだけ
// ★ リクエストとレスポンスはこの中にネストして Req / Res と名付ける
public interface ICreateUserCommandService
    : ICommandService<ICreateUserCommandService.Req, ICreateUserCommandService.Res>
{
    sealed record Req(string UserName, OperateInfo OperateInfo) : ICommandServiceDTO;

    sealed record Res(Guid UserId) : ICommandServiceDTO;
}

// ★ 型引数の名前は TSession ではなく TUserSession。理由は下記
public sealed class CreateUserCommandService<TUserSession> : ICreateUserCommandService
    where TUserSession : IDisposable
{
    private readonly ITransactionManager _transactionManager;

    // ★ 集約サービスも具象ではなくインターフェースで受ける
    private readonly IUserAggregateService<TUserSession> _userAggregateService;

    public CreateUserCommandService(
        ITransactionManager transactionManager,
        IUserAggregateService<TUserSession> userAggregateService)
    {
        _transactionManager = transactionManager;
        _userAggregateService = userAggregateService;
    }

    public async ValueTask<ICreateUserCommandService.Res> ExecuteAsync(
        ICreateUserCommandService.Req req, CancellationToken cancellationToken = default)
    {
        var userId = UserId.New();

        await _transactionManager.ExecuteTransactionAsync<TUserSession>(
            async (sessions, token) =>
            {
                var session = sessions.GetSession<TUserSession>();

                // 外部入力を値オブジェクトへ。不正なら Create が例外を投げる（型はプロジェクト側で定義する）
                var userName = UserName.Create(req.UserName);

                var user = User.Create(userId, userName);
                await _userAggregateService.RegisterAsync(session, user, req.OperateInfo, token);
            },
            cancellationToken: cancellationToken);

        return new ICreateUserCommandService.Res(userId.Value);
    }
}
```

`ExecuteTransactionAsync` の中身は「全部成功するか、全部無かったことになるか」の単位。
本体が例外を投げれば自動でロールバックされ、最後まで通ればコミットされる。
本体の中で commit / rollback / dispose を呼んではいけない。

**レスポンスにエンティティを入れない。** エンティティは読み出したトランザクションのもので、
呼び出し側に届く頃にはセッションは `Dispose` 済み。DTO に詰め替える。

**ユースケースで型引数を `TSession` と名付けない。** ユースケースは複数の集約に触るのが普通で、
集約ごとにデータストアが違えばセッション型も違う。集約の名前を入れた `TUserSession` にしておけば、
2 つ目の集約が加わったときに
`ExecuteTransactionAsync<TUserSession, TOrderSession>` と並べられる。
今 1 集約しか扱っていなくても最初からこう書くのがよい
（→ [architecture.md](architecture.md#型引数の名前-集約をまたぐ層では-t集約名session)）。

**リクエスト / レスポンス DTO は型引数を取らない。** 境界の DTO はセッションの存在を
呼び出し側に知らせないためにあるので、ここに型引数が現れたら設計が漏れている。

**ユースケースごとに、セッション型引数を持たないインターフェースを立てる。**
上の `ICreateUserCommandService` がそれで、プレゼンテーション層はこれを解決して呼ぶだけなので
`<AppSession>` を書かずに済む（テストでも差し替えるだけで済む）。
注入する下の層も同じ理由で口で受ける — 集約サービスは `IUserAggregateService<TUserSession>`、
ドメインサービスは `IUserNameUniquenessService<TUserSession>`。

**リクエストとレスポンスは口の中にネストする。** `ICommandService` を継承した時点で
「メソッドは 1 つ、リクエスト 1 型、レスポンス 1 型」が確定しているので、DTO は口と 1 対 1 に対応する。
中に置けば `ICreateUserCommandService.Req` と口から必ず辿れて、
別のユースケースの DTO を渡す事故も起きない
（→ [use-case.md](use-case.md#リクエストとレスポンスは口の中にネストする)）。

### クエリサービス（読み込み）

**リポジトリを通さず、ストアを直接読む。** クエリサービスにも口を立て、レスポンスをネストする。

```csharp
public interface IListUsersQueryService : IQueryService<IListUsersQueryService.Res>
{
    sealed record Res(IReadOnlyList<UserSummary> Users) : IQueryServiceDTO;
}

public sealed class ListUsersQueryService : IListUsersQueryService
{
    private readonly AppDatabase _database;

    public ListUsersQueryService(AppDatabase database) => _database = database;

    public ValueTask<IListUsersQueryService.Res> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var users = _database.Users
            .Select(row => new UserSummary(row.Id, row.Name))
            .ToList();

        return ValueTask.FromResult(new IListUsersQueryService.Res(users));
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

var createUser = scope.ServiceProvider.GetRequiredService<ICreateUserCommandService>();
var response = await createUser.ExecuteAsync(
    new ICreateUserCommandService.Req("alice", OperateInfo.Now("operator-1")));

Console.WriteLine(response.UserId);
```

---

## つまずいたら

| 症状 | 原因 |
|---|---|
| `InvalidOperationException: No transaction service is registered...` | `ITransactionService<TSession>` の登録漏れ（Step 6） |
| `TransactionSessionNotFoundException` | `ExecuteTransactionAsync<T>` に渡していないセッション型を `GetSession` した |
| `NestedTransactionException` | 実行中のトランザクションの中で `ExecuteTransactionAsync` を呼んだ（コマンドサービスから別のコマンドサービスを呼んだ） |
| 負荷時だけセッションが混ざる | `TransactionManager` を Singleton で登録している（Step 6） |
| `TryGetValue` が true なのに `NullReferenceException` | リポジトリで `return null;` と書いた → `Optional<T>.Empty` にする |
| セッションが二重に `Dispose` される | `ITransactionService` の実装側で `Dispose` を呼んでいる（Step 5） |
| コミットしたのにデータが無い | `ExecuteTransactionAsync` の外で `SaveAsync` を呼んでいる |
| 2 つ目のストアだけロールバックされない | 複数セッションの commit はアトミックではない（仕様） |
| ドメイン層がインフラ層に依存してしまった | リポジトリのインターフェースに `AppSession` と書いた → `TSession` で開く（Step 4） |

## 次に読むもの

- [architecture.md](architecture.md) — **なぜ `TSession` を引き回すのか**
- [best-practices.md](best-practices.md) — 規約の一覧
- [domain-model.md](domain-model.md) — エンティティ / 値オブジェクト / リポジトリ
- [use-case.md](use-case.md) — 3 種のサービスの使い分け
- [optional.md](optional.md) — `Optional<T>` の三状態
- [../samples/](../samples/) — 動く完成品
