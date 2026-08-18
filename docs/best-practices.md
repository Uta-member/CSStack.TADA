# ベストプラクティス

**放置すると必ず踏む規約を 1 箇所に集めたもの。** 各項目は
「間違い → 正しい形 → なぜ」の順で、単体で読めるように書いてある。

背景の説明は既存のドキュメントに書いてあるので、ここでは繰り返さずリンクする。

- なぜこの設計なのか → [architecture.md](architecture.md)
- エンティティ / 値オブジェクト / リポジトリ → [domain-model.md](domain-model.md)
- 3 種のサービスとトランザクション → [use-case.md](use-case.md)
- `Optional<T>` の全体像 → [optional.md](optional.md)
- 動く実例 → [../samples/](../samples/)

---

## 危険度順の要約

| # | 規約 | 破ったときの壊れ方 |
|---|---|---|
| 1 | `Optional<T>` で `return null;` と書かない | `NullReferenceException`。しかも `HasValue` は true |
| 2 | `TransactionManager` は **Scoped** で登録する | 本番の同時実行時だけセッションが混線する |
| 3 | `ITransactionService` の実装側でセッションを `Dispose` しない | 二重解放 |
| 4 | 複数セッションの commit はアトミックではない | 片方だけ確定したまま残る |
| 5 | トランザクションを開始してよいのは `ICommandService` だけ | 意図しない粒度でコミットされる／入れ子は `NestedTransactionException` |
| 6 | リポジトリは「見つからない」を例外にしない | 正常な不在が例外になる |
| 7 | `IRepository` に検索系メソッドを足さない | 集約の境界が読み取り側へ漏れる |
| 8 | 検証は `Create` に書き、`Reconstruct` では検証しない | 古いデータが読み戻せなくなる |
| 9 | 値オブジェクトは `record` で実装する | 値が等しいのに等価にならない |
| 10 | namespace は `CSStack.TADA` フラット | ビルドは通るが規約から外れる |
| 11 | ドメイン層・ユースケース層に具体的なセッション型を書かない | 依存の向きが逆転し、TADA と DDD の利点が消える |
| 12 | 集約サービスとユースケースはインターフェースを立ててから実装する | テストが具象クラスとリポジトリ実装の組み立てになる／型引数が呼び出し側に漏れる |
| 13 | 集約サービスに `SaveAsync` のような汎用的な操作を置かない | 呼ぶ側が汎用のほうを選び、集約のルールが素通りされる |
| 14 | リクエスト / レスポンスはサービスの口の中にネストする | 口と DTO の対応が保証されず、別ユースケースの DTO を渡しても通る |

---

## 1. `Optional<T>` で `return null;` と書かない

**このライブラリで最も踏まれる地雷。**

```csharp
// ✗ 間違い
public async ValueTask<Optional<User>> FindByIdentifierAsync(...)
{
    var row = await session.FindAsync(identifier.Value, cancellationToken);
    return null;   // ← Some(null) になる。None ではない
}
```

```csharp
// ✓ 正しい
public async ValueTask<Optional<User>> FindByIdentifierAsync(...)
{
    var row = await session.FindAsync(identifier.Value, cancellationToken);
    return row is null
        ? Optional<User>.Empty
        : Optional<User>.Some(User.Reconstruct(row));
}
```

**なぜ:** `Optional<T>` には `TValue` からの暗黙変換があるので、`null` は
`None` ではなく **`Some(null)`** になる。`HasValue` は `true` のままなので、
呼び出し側の `TryGetValue` は `true` を返し、その直後に
`NullReferenceException` になる。不在を表すのは `Optional<T>.Empty`。

**型引数の null 許容性も正しく宣言する。** null が値として正当なら
`Optional<string?>`、そうでなければ `Optional<User>`。
コンパイラの null 許容解析はこの宣言に従う。

→ [optional.md](optional.md#最大の罠-return-null-は-none-ではない)

---

## 2. `TransactionManager` は Scoped で登録する

```csharp
// ✗ 間違い
services.AddSingleton<ITransactionManager, TransactionManager>();
```

```csharp
// ✓ 正しい
services.AddScoped<ITransactionManager, TransactionManager>();
services.AddScoped<ITransactionService<AppSession>, AppTransactionService>();
```

**なぜ:** `TransactionManager` は実行中のセッションを可変フィールドに保持し、
スレッドセーフではない。Singleton にすると全リクエストが同じインスタンスを共有し、
セッションが混線する。**単体テストでは同時実行しないので気づかず、
本番の負荷時にだけ壊れる**のがたちの悪いところ。

同じ理由で、1 つのインスタンスを複数スレッドや並行 `Task` から同時に動かしてもいけない。

**`ITransactionService<TSession>` の登録も忘れないこと。** 登録が無いと
`TransactionManager` は即 `InvalidOperationException` を投げる。
セッション型が複数あるならその数だけ登録する。

→ [getting-started.md](getting-started.md#step-6-di-を登録する)

---

## 3. `ITransactionService` の実装側でセッションを `Dispose` しない

```csharp
// ✗ 間違い
public ValueTask CommitAsync(AppSession session, CancellationToken cancellationToken = default)
{
    session.Commit();
    session.Dispose();   // ← 二重解放になる
    return ValueTask.CompletedTask;
}
```

```csharp
// ✓ 正しい
public ValueTask CommitAsync(AppSession session, CancellationToken cancellationToken = default)
{
    session.Commit();
    return ValueTask.CompletedTask;
}
```

**なぜ:** **セッションの所有権は `ITransactionManager` にある。**
マネージャーが begin したセッションは、commit 後・rollback 後・例外時の
いずれの経路でもマネージャーが `Dispose` する。
`ITransactionService<TSession>` の仕事は begin / commit / rollback だけ。

同じ理由で、`ExecuteTransactionAsync` の本体の中でも
commit / rollback / dispose を呼んではいけない。

---

## 4. 複数セッションの commit はアトミックではない

```csharp
// これは「両方成功するか、両方無かったことになるか」ではない
await _transactionManager.ExecuteTransactionAsync<TAccountSession, TAuditLogSession>(
    async (sessions, token) => { /* ... */ },
    cancellationToken: cancellationToken);
```

**なぜ:** 2 相コミットではない。セッションは順番に commit されるので、
**2 つ目の commit が失敗しても 1 つ目は確定したまま残る。**
マネージャーは未 commit のセッションだけロールバックを試みるが、
確定済みのものは元に戻せない。

対処:

- **確定してほしいストアが 1 つだけになるよう設計する**（推奨）
- どうしても複数必要なら、操作を冪等・再実行可能にする
- 補償トランザクションを自前で用意する

なお **ロールバックはキャンセルされない。** `CancellationToken` がキャンセル済みでも
ロールバックは実行されるので、キャンセルによってトランザクションが開いたまま残ることはない。

---

## 5. トランザクションを開始してよいのは `ICommandService` だけ

```csharp
// ✗ 間違い: 集約サービスやドメインサービスが ITransactionManager を注入している
public sealed class UserAggregateService
{
    public UserAggregateService(ITransactionManager transactionManager) { }
}
```

```csharp
// ✓ 正しい: セッションを引数で受け取る（型は型引数のまま。規約 11 も参照）
public sealed class UserAggregateService<TSession>
    where TSession : IDisposable
{
    public ValueTask RenameAsync(
        TSession session, UserId identifier, UserName newName, OperateInfo operateInfo,
        CancellationToken cancellationToken = default)
    { }
}
```

**なぜ:** トランザクションの境界はユースケースの粒度と一致していなければならず、
それを知っているのは `ICommandService` だけ。下の層が勝手に開始すると、
呼び出され方によって粒度が変わってしまう。

`ICommandService` が `ITransactionManager` を注入して `ExecuteTransactionAsync` で包み、
取り出したセッションを下の層へ渡す。**それより下の層はトランザクションを開始しない。**

ドメインサービスは `ExecuteAsync` にセッション引数が無いので、
**DTO にセッションを載せて渡す**（ドメインサービスの DTO がセッションを持ってよい唯一の理由）。

### 系: コマンドサービスから別のコマンドサービスを呼ばない

```csharp
// ✗ 間違い: トランザクションが入れ子になる（NestedTransactionException）
public sealed class RegisterUserCommandService<TUserSession> : ICommandService<RegisterUserDTO>
    where TUserSession : IDisposable
{
    public async ValueTask ExecuteAsync(RegisterUserDTO req, CancellationToken cancellationToken = default)
    {
        await _transactionManager.ExecuteTransactionAsync<TUserSession>(
            async (sessions, token) =>
            {
                // この中でさらに ExecuteTransactionAsync を呼ぶ
                await _sendWelcomeMailCommandService.ExecuteAsync(new SendWelcomeMailDTO(...), token);
            },
            cancellationToken: cancellationToken);
    }
}
```

```csharp
// ✓ 正しい: 共有したい処理をドメインサービスに切り出し、セッションを渡して呼ぶ
await _transactionManager.ExecuteTransactionAsync<TUserSession>(
    async (sessions, token) =>
    {
        var session = sessions.GetSession<TUserSession>();
        await _registerUserDomainService.ExecuteAsync(new RegisterUserDomainDTO(session, ...), token);
        await _sendWelcomeMailDomainService.ExecuteAsync(new SendWelcomeMailDomainDTO(session, ...), token);
    },
    cancellationToken: cancellationToken);
```

**なぜ:** `ITransactionManager` は Scoped 登録なので、コマンドサービスが別のコマンドサービスを
呼ぶと**同じマネージャーインスタンス**に行き着く。マネージャーはセッションを
「どの `ExecuteTransactionAsync` が開始したか」の区別なしに保持しているため、
内側の commit が外側のセッションまで確定して `Dispose` してしまう。
v3.0.1 からはこれを検知して `NestedTransactionException` を投げる。

**連続して**（入れ子でなく）呼ぶのは正当。1 つ目のトランザクションが終わってから
2 つ目を開始する分には何も起きない。

→ [use-case.md](use-case.md#コマンドサービスがトランザクションの境界)

---

## 6. リポジトリは「見つからない」を例外にしない

```csharp
// ✗ 間違い: リポジトリが不在を異常と決めつけている
public async ValueTask<Optional<User>> FindByIdentifierAsync(...)
{
    var row = await session.FindAsync(...);
    if (row is null)
    {
        throw new UserNotFoundException(identifier.Value);
    }
}
```

```csharp
// ✓ 正しい: リポジトリは Empty を返し、
//           「無かったら失敗」を決めるのは集約サービス / ユースケース
//           （集約サービスの中の private ヘルパー。口には載せない → 規約 13）
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
```

**なぜ:** 「見つからない」が異常かどうかは操作次第。削除済みのものをもう一度削除するのは
問題ないが、無い口座から引き落とすのは問題。**それを知っているのは呼び出し側だけ。**

同じ理由で `SaveAsync` も「既に居る」を例外にしない（upsert なので、
既に居ることは失敗ではない）。「既に居たら失敗」も上位層で先に読んで判断する。

**`UserNotFoundException` のような例外は TADA が提供するのではなく、プロジェクト自身が定義する。**
対象を特定できる情報（識別子など）をプロパティとして持たせておくと、
呼び出し側でメッセージを組み立てなくてもログに出る。

```csharp
throw new UserNotFoundException(identifier.Value);
```

→ [domain-model.md](domain-model.md#例外を投げる層)

---

## 7. `IRepository` に検索系メソッドを足さない

```csharp
// ✗ 間違い
public interface IUserRepository<TSession> : IRepository<User, UserId, OperateInfo, TSession>
    where TSession : IDisposable
{
    ValueTask<IReadOnlyList<User>> FindAllAsync(TSession session, CancellationToken ct = default);
    ValueTask<IReadOnlyList<User>> FindByNameAsync(TSession session, string name, CancellationToken ct = default);
}
```

```csharp
// ✓ 正しい: 一覧・検索はクエリサービスへ。ストアを直接読み、DTO を返す
public sealed class SearchUsersQueryService : ISearchUsersQueryService
{
    public ValueTask<ISearchUsersQueryService.Res> ExecuteAsync(
        ISearchUsersQueryService.Req req, CancellationToken ct = default)
    { }
}
```

**なぜ:** リポジトリにあるのは `FindByIdentifierAsync` と `SaveAsync`（upsert）だけ
（削除するなら `IRepositoryDeletable` で `DeleteAsync` が加わる）。
エンティティは**書き込みモデル**であり、それを組み立ててから一覧用に潰すのは
無駄な上に、集約の境界を読み取り側へ引きずり出すことになる。

クエリサービスはリポジトリもエンティティも通さず、ストアを直接読んで DTO を返す。

→ [use-case.md](use-case.md#クエリサービスはリポジトリを通さない)

---

## 8. 検証は `Create` に書き、`Reconstruct` では検証しない

```csharp
// ✗ 間違い: Reconstruct でも検証している
public static UserName Reconstruct(string value) => Create(value);
```

```csharp
// ✓ 正しい
public static UserName Create(string value)
{
    CheckInvariants(value);   // 検証はここだけ
    return new UserName(value);
}

public static UserName Reconstruct(string value) => new(value);   // 検証しない

private static void CheckInvariants(string value)
{
    if (value.Length < MinLength || value.Length > MaxLength)
    {
        throw new UserNameLengthException(
            minLength: MinLength, maxLength: MaxLength, currentLength: value.Length);
    }
}
```

**なぜ:** `Reconstruct` は永続化からの復元用。**ルールを厳しくした後でも、
古いルールで保存されたデータを読み戻せるようにするため**に検証しない。
`Create` を通してしまうと、仕様変更のたびに既存データが読めなくなる。

- `Create` を呼ぶ: ユーザー入力、外部 API、その他信用できない入力
- `Reconstruct` を呼ぶ: リポジトリが永続化された値から復元するときだけ

**`IValueObject.Validate()` は `Create` の代わりではない。** `Validate()` は既定実装のない
必須メンバーなので実装は必要だが、`Create` を通った時点で検証済みなので `Create` の中から
呼ぶ必要はない。`Validate()` を用意したのは、あるインスタンスが `Create` を通ったかどうかを
外部から検証する術が無いため。`Reconstruct` の後に使うのはその一例に過ぎず、`Validate()` 自体は
何にも依存しない——単に今の値が現行の不変条件を満たしているかを確認するだけのプリミティブ。
**`Create` と `Validate` の検証は同じ `private` ヘルパーに集約する**（`Create` はそのヘルパーを
呼んで新しいインスタンスを作り、`Validate` は同じヘルパーを既存の `Value` に対して呼ぶ）。
コンストラクタを private にして、`Create` / `Reconstruct` だけを入口にする。

→ [domain-model.md](domain-model.md#検証は-create-の中に書く-validate-は別経路)

---

## 9. 値オブジェクトは `record` で実装する

```csharp
// ✗ 間違い: class だと参照等価のまま
public sealed class UserName : ISingleValueObject<string, UserName>
```

```csharp
// ✓ 正しい
public sealed record UserName : ISingleValueObject<string, UserName>
```

**なぜ:** 値オブジェクトは「同じ値を持つ 2 つのインスタンスは同じもの」であるべきだが、
`class` では参照等価のままなので同じ値でも等価にならない。
`record` なら値等価・`GetHashCode`・`ToString`・`with` がすべて手に入る。

`IValueObject` はマーカーなのでこれを強制できない。**この規約を守らせるものは無く、
破っても気づけないバグになる**ので、明示的に規約にしてある。

`ValueObjectBase` は存在しない（v2.0.0 で削除。`record` のほうが適切なため）。

→ [domain-model.md](domain-model.md#record-で実装する)

---

## 10. namespace は `CSStack.TADA` フラット

```csharp
// ✗ 間違い
namespace CSStack.TADA.Domain { }
```

```csharp
// ✓ 正しい（ライブラリ本体に手を入れる場合）
namespace CSStack.TADA { }
```

**なぜ:** `Domain/` `UseCase/` などのフォルダ構成を namespace に反映しない。
利用者が `using CSStack.TADA;` の 1 行だけで済むようにするための意図的な設計で、
`.editorconfig` の `dotnet_style_namespace_match_folder = false` がこれに対応する。

ブロックスコープの namespace（`namespace X { ... }`）を使う。file-scoped ではない。

**利用者側のコードにこの規約は及ばない。** 自分のアプリの namespace は自由に決めてよい。

---

## 11. ドメイン層・ユースケース層に具体的なセッション型を書かない

```csharp
// ✗ 間違い: ドメイン層がインフラ層の実トランザクション因子を名指ししている
public interface IUserRepository : IRepositoryDeletable<User, UserId, OperateInfo, AppSession>;

public sealed class UserAggregateService
    : AggregateServiceBase<User, UserId, IUserRepository, OperateInfo, AppSession>;

public sealed class CreateUserCommandService : ICreateUserCommandService
{
    // 本体で ExecuteTransactionAsync<AppSession> を呼んでいる
}
```

```csharp
// ✓ 正しい: 型引数として外から受け取る
//   ドメイン層（集約が扱うリポジトリは 1 つなので TSession でよい）
public interface IUserRepository<TSession> : IRepositoryDeletable<User, UserId, OperateInfo, TSession>
    where TSession : IDisposable;

public sealed class UserAggregateService<TSession>
    : AggregateServiceBase<User, UserId, IUserRepository<TSession>, OperateInfo, TSession>
    where TSession : IDisposable;

//   ユースケース層（集約をまたぐので T[集約名]Session）
public sealed class CreateUserCommandService<TUserSession> : ICreateUserCommandService
    where TUserSession : IDisposable
{
    // ExecuteTransactionAsync<TUserSession> / sessions.GetSession<TUserSession>()
}

//   インフラ層。具体型を名指しするのは実装だけ
public sealed class InMemoryUserRepository : IUserRepository<AppSession>;

//   プレゼンテーション層。型が確定するのはここだけ（口の立て方は規約 12）
services.AddScoped<IUserRepository<AppSession>, InMemoryUserRepository>();
services.AddScoped<IUserAggregateService<AppSession>, UserAggregateService<AppSession>>();
services.AddScoped<ICreateUserCommandService, CreateUserCommandService<AppSession>>();
```

**なぜ:** 間違いのほうもコンパイルは通るし、小さなアプリなら動く。しかし
**ドメイン層がインフラ層の型を名指しした時点で依存の向きが逆転し、TADA を採用する利点と
DDD の利点がほぼ消える。** ストアを載せ替えるたびにドメイン層を書き換えることになり、
ドメインの単体テストにも毎回セッションの実物が必要になり、
集約ごとにストアを分ける構成へも進めなくなる。

`TSession` を全レイヤーに引き回すというのは、**型引数を引き回すこと**であって、
**具体型をドメインに焼き込むこと**ではない。

**型引数の名前**にも規約がある。

| 層 | 名前 | 理由 |
|---|---|---|
| リポジトリ・集約サービス | `TSession` | 集約が扱うリポジトリは 1 つなので、区別する必要がない |
| ドメインサービス・ユースケース | **`T[集約名]Session`** | 複数の集約に触りうる。集約ごとにストアが違えばセッション型も違う |

**今 1 集約しか扱っていなくても `TUserSession` と書く。** 後から集約が増えたときに
`TSession` がどの集約のものだったか判別できず、改名が必要になる。
2 つ目が加わっても `ExecuteTransactionAsync<TUserSession, TOrderSession>` と並べるだけで済む。

なお **境界の DTO（`ICommandServiceDTO` / `IQueryServiceDTO`）は型引数を持たない。**
セッションを載せないのだから型引数も要らず、現れたらセッションの存在が
呼び出し側へ漏れている。セッションを載せるドメインサービスの `Req` だけが型引数を取る。

→ [architecture.md](architecture.md#ドメイン層に具体的なセッション型を書かない)

---

## 12. 集約サービスとユースケースはインターフェースを立ててから実装する

```csharp
// ✗ 間違い: 基底クラスを継承した具象クラスをそのまま注入・解決している
public sealed class UserAggregateService<TSession>
    : AggregateServiceBase<User, UserId, IUserRepository<TSession>, OperateInfo, TSession>
    where TSession : IDisposable { /* ... */ }

public sealed class CreateUserCommandService<TUserSession>
    : ICommandService<CreateUserReq, CreateUserRes>
    where TUserSession : IDisposable
{
    // 具象の集約サービスを受け取っている
    public CreateUserCommandService(
        ITransactionManager transactionManager,
        UserAggregateService<TUserSession> userAggregateService) { /* ... */ }
}

// 呼び出し側にセッション型が漏れる
services.AddScoped<UserAggregateService<AppSession>>();
services.AddScoped<ICommandService<CreateUserReq, CreateUserRes>,
    CreateUserCommandService<AppSession>>();
```

```csharp
// ✓ 正しい: 集約サービスは IAggregateService を継承した口を立て、その実装を書く
public interface IUserAggregateService<TSession>
    : IAggregateService<User, UserId, IUserRepository<TSession>, OperateInfo, TSession>
    where TSession : IDisposable
{
    ValueTask RegisterAsync(
        TSession session, User user, OperateInfo operateInfo, CancellationToken ct = default);
}

public sealed class UserAggregateService<TSession>
    : AggregateServiceBase<User, UserId, IUserRepository<TSession>, OperateInfo, TSession>,
    IUserAggregateService<TSession>
    where TSession : IDisposable { /* ... */ }

// ✓ ユースケースは「セッション型引数を持たない口」を立て、DTO はその中にネストする（規約 14）
public interface ICreateUserCommandService
    : ICommandService<ICreateUserCommandService.Req, ICreateUserCommandService.Res>
{
    sealed record Req(string UserName, OperateInfo OperateInfo) : ICommandServiceDTO;

    sealed record Res(Guid UserId) : ICommandServiceDTO;
}

public sealed class CreateUserCommandService<TUserSession> : ICreateUserCommandService
    where TUserSession : IDisposable
{
    // ✓ 下の層も口で受ける
    public CreateUserCommandService(
        ITransactionManager transactionManager,
        IUserAggregateService<TUserSession> userAggregateService,
        IUserNameUniquenessService<TUserSession> uniquenessService) { /* ... */ }
}

services.AddScoped<IUserAggregateService<AppSession>, UserAggregateService<AppSession>>();
services.AddScoped<ICreateUserCommandService, CreateUserCommandService<AppSession>>();
```

```csharp
// プレゼンテーション層。セッション型がどこにも出てこない
var commandService = scope.ServiceProvider.GetRequiredService<ICreateUserCommandService>();
await commandService.ExecuteAsync(new ICreateUserCommandService.Req("alice", operateInfo));
```

**なぜ:** 理由は 2 つある。

1. **テスト。** ユースケースが具象の集約サービスを受け取っていると、ユースケースのテストで
   集約サービスの具象クラスを組み立てることになり、そのコンストラクタが要求する
   リポジトリ実装まで用意する話になる。口で受けていれば差し替えるだけで済む
2. **型引数を呼び出し側に見せない。** 実装はセッション型を型引数に持つので、
   `ICommandService<TReq, TRes>` で解決するとプレゼンテーション層が
   `CreateUserCommandService<AppSession>` を名指しすることになる。
   セッション型引数を持たない口を立てておけば、型引数が現れるのは DI 登録の 1 行だけになる

`AggregateServiceBase` は**実装の詳細**であって、上の層に見せる契約ではない。

**4 種すべてに口を立てる。理由は層によって違う。**

| 層 | 口を立てる理由 |
|---|---|
| 集約サービス | 基底クラスの継承を隠す。テストで差し替える |
| コマンドサービス | セッション型引数を呼び出し側から隠す |
| ドメインサービス | TADA 由来の共通インターフェースは無く、注入だけなら口は不要。**リクエストの置き場所**として立てる（規約 14） |
| クエリサービス | セッション型引数は無い。**レスポンス型をそのクエリに固定する**ため（規約 14） |

→ [use-case.md](use-case.md#ユースケースにはセッション型引数を持たない口を立てる)、
[domain-model.md](domain-model.md#インターフェースを立ててから実装する)

---

## 13. 集約サービスに `SaveAsync` のような汎用的な操作を置かない

```csharp
// ✗ 間違い: 汎用の SaveAsync が RegisterAsync と並んでいる
public interface IUserAggregateService<TSession>
    : IAggregateService<User, UserId, IUserRepository<TSession>, OperateInfo, TSession>
    where TSession : IDisposable
{
    ValueTask<User> GetRequiredAsync(TSession s, UserId id, CancellationToken ct = default);
    ValueTask RegisterAsync(TSession s, User u, OperateInfo o, CancellationToken ct = default);
    ValueTask SaveAsync(TSession s, User u, OperateInfo o, CancellationToken ct = default);
}

// 呼ぶ側はこう書ける。RegisterAsync が持っていた「既に居たら失敗」は素通り
var user = User.Create(userId, userName);
await _userAggregateService.SaveAsync(session, user, operateInfo, token);
```

```csharp
// ✓ 正しい: 並ぶのはドメインの操作だけ。読み込み → 変更 → 保存は 1 つの操作に閉じる
public interface IUserAggregateService<TSession>
    : IAggregateService<User, UserId, IUserRepository<TSession>, OperateInfo, TSession>
    where TSession : IDisposable
{
    ValueTask DeleteAsync(TSession s, UserId id, OperateInfo o, CancellationToken ct = default);
    ValueTask RegisterAsync(TSession s, User u, OperateInfo o, CancellationToken ct = default);
    ValueTask RenameAsync(
        TSession s, UserId id, UserName newName, OperateInfo o, CancellationToken ct = default);
}

// 実装側。GetRequiredAsync は private に留め、エンティティを上の層へ出さない
public async ValueTask RenameAsync(
    TSession session, UserId identifier, UserName newName, OperateInfo operateInfo,
    CancellationToken cancellationToken = default)
{
    var user = await GetRequiredAsync(session, identifier, cancellationToken);
    user.Rename(newName);
    await Repository.SaveAsync(session, user, operateInfo, cancellationToken);
}
```

**なぜ:** `IAggregateService` を継承した口は**実質的に集約ルート**で、
そこに並ぶメソッドが「この集約に何ができるか」の一覧になる。
`SaveAsync` は何でも受け取るので、`RegisterAsync` と並べれば呼ぶ側はたいてい `SaveAsync` を選び、
**`RegisterAsync` に置いたルール（既に居たら失敗）が素通りされる。**
分けた意味が消えるだけでなく、リポジトリの `SaveAsync` が upsert であるために黙って上書きになる。

「取得して、変更して、保存する」を `RenameAsync` のような 1 つの操作に閉じれば:

- 名前がそのままドメインの語彙になる（何が起きるのか読める）
- エンティティを集約の外へ出さずに済む。**外に出すと、書き換えても保存する手段が口に無い**
  （`GetRequiredAsync` を口に載せない理由でもある。読み取り目的ならクエリサービスの仕事）
- 保存忘れが起こりえない

リポジトリの `SaveAsync` を呼ぶのは集約サービスまで。ユースケースからは呼ばない。

→ [domain-model.md](domain-model.md#インターフェースを立ててから実装する)

---

## 14. リクエスト / レスポンスはサービスの口の中にネストする

```csharp
// ✗ 間違い: DTO が名前空間に平らに並んでいる
public sealed record CreateUserReq(string UserName, OperateInfo OperateInfo) : ICommandServiceDTO;
public sealed record CreateUserRes(Guid UserId) : ICommandServiceDTO;

public interface ICreateUserCommandService : ICommandService<CreateUserReq, CreateUserRes>;
```

```csharp
// ✓ 正しい: 口の中に Req / Res としてネストする
public interface ICreateUserCommandService
    : ICommandService<ICreateUserCommandService.Req, ICreateUserCommandService.Res>
{
    sealed record Req(string UserName, OperateInfo OperateInfo) : ICommandServiceDTO;

    sealed record Res(Guid UserId) : ICommandServiceDTO;
}

// ドメインサービスも同じ考え方。継承する共通インターフェースは無いので ExecuteAsync を自分で宣言する。
// セッションは口の型引数をそのまま使う
public interface IUserNameUniquenessService<TUserSession>
    where TUserSession : IDisposable
{
    sealed record Req(TUserSession Session, UserName Name, UserId ExceptUserId);

    ValueTask ExecuteAsync(Req req, CancellationToken cancellationToken = default);
}

// クエリサービスも同じ
public interface ISearchUsersQueryService
    : IQueryService<ISearchUsersQueryService.Req, ISearchUsersQueryService.Res>
{
    sealed record Req(string NamePrefix) : IQueryServiceDTO;

    sealed record Res(IReadOnlyList<UserSummary> Users) : IQueryServiceDTO;
}
```

**なぜ:** `ICommandService` / `IQueryService` を継承した時点で（ドメインサービスは
`ExecuteAsync` を自分で宣言した時点で）**「メソッドは 1 つ、リクエスト 1 型、レスポンス 1 型」が
確定している。** DTO は口と 1 対 1 に対応するのだから、口から辿れる場所に置くのが自然な帰結になる。

- **口から辿れる。** `ICreateUserCommandService.Req` は必ずそこにある。
  平らに並んだ `~Req` 群を名前で探さなくてよい
- **対応が固定される。** 外に置くと、別のユースケースのリクエストを渡しても
  型さえ合えばコンパイルが通る
- **名前が短い。** ユースケースが増えても DTO 名の接頭辞が伸び続けない

レスポンスが要らないなら `Res` を作らない（`ICommandService<TReq>` を継承する）。
**複数の口で共有する読み取りモデル**（一覧の 1 行など）は 1 対 1 ではないので、ネストせず外に置く。

→ [use-case.md](use-case.md#リクエストとレスポンスは口の中にネストする)

---

## その他の細かい規約

### 長さの例外は名前付き引数で投げる

値オブジェクトの長さ検証で `MinLength` / `MaxLength` / `CurrentLength` の 3 つを保持する
自前の例外（`UserNameLengthException` など）を定義する場合、この 3 つは**すべて `int`** になる。

```csharp
// ✗ 順番を間違えてもコンパイルが通り、間違った境界値がログに出る
throw new UserNameLengthException(value.Length, MinLength, MaxLength);

// ✓
throw new UserNameLengthException(
    minLength: MinLength, maxLength: MaxLength, currentLength: value.Length);
```

引数が 3 つとも `int` なので型では守れない。順番は
`minLength` → `maxLength` → `currentLength`（宣言された上下限が先、弾かれた長さが最後）。

### レスポンス DTO にエンティティを入れない

エンティティは読み出したトランザクションのもので、
呼び出し側に届く頃にはセッションは `Dispose` 済み。DTO に詰め替える。

### DTO は `record` で宣言し、口の中にネストする

`ICommandServiceDTO` / `IQueryServiceDTO` はいずれもマーカーで、
コマンドサービスとクエリサービスが互いの DTO を受け取ってしまうのを防ぐためだけにある
（ドメインサービスにはこの種のマーカーが無い。→ [domain-model.md](domain-model.md#ドメインサービス)）。
置き場所はそれを使うサービスの口の中（`Req` / `Res`）→ 規約 14。

| DTO | セッションを持つか | エンティティを持つか |
|---|---|---|
| `ICommandServiceDTO` | **持たない**（自分で開始する） | 持たない |
| `IQueryServiceDTO` | 未コミットを読む必要があるときだけ | 持たない |
| ドメインサービスの `Req`（マーカー無し） | **持つ**（伝える経路が他に無い） | 持ってよい |

### `IQueryService<TRes>` の型引数はレスポンス

`ICommandService<TReq>` の 1 つ目はリクエストだが、
`IQueryService<TRes>` の 1 つ目は**レスポンス**。引数があるときは
迷わず `IQueryService<TReq, TRes>` を使えばよい。

→ [use-case.md](use-case.md#ジェネリック型引数の罠)

---

## ライブラリ本体に手を入れるとき

利用者側には関係しないが、このリポジトリを触るなら追加で:

- **インデント**: `src/` はタブ、`tests/` と `samples/` はスペース 4
- **`.cs` は UTF-8 BOM 付き**、`.md` は BOM なし
- **すべての公開メンバーに XML doc が必須**。`TreatWarningsAsErrors` が有効なので
  記述漏れ（CS1591）や破損（CS1570 / CS1574）は**ビルドエラーになる**
- 型のメンバーはおおむねアルファベット順。既存の並びを崩さない
- 言語: XML doc は英語 / README は日英併記 / `docs/`・CHANGELOG・
  テストメソッド名・コミットメッセージは日本語
- **仕様を変えたら XML doc と `docs/` の両方を更新する**

## 関連

- [architecture.md](architecture.md) — なぜこの設計なのか
- [getting-started.md](getting-started.md) — ゼロから動かすまで
- [domain-model.md](domain-model.md) — ドメインモデルの規約
- [use-case.md](use-case.md) — ユースケース層とトランザクション
- [optional.md](optional.md) — `Optional<T>` の三状態
- [api-reference.md](api-reference.md) — 公開型の一覧
