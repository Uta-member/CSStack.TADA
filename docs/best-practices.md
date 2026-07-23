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
| 5 | トランザクションを開始してよいのは `ICommandService` だけ | 意図しない粒度でコミットされる |
| 6 | リポジトリは `ObjectNotFoundException` を投げない | 正常な不在が例外になる |
| 7 | `IRepository` に検索系メソッドを足さない | 集約の境界が読み取り側へ漏れる |
| 8 | 検証は `Create` に書き、`Reconstruct` では検証しない | 古いデータが読み戻せなくなる |
| 9 | 値オブジェクトは `record` で実装する | 値が等しいのに等価にならない |
| 10 | namespace は `CSStack.TADA` フラット | ビルドは通るが規約から外れる |

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
await _transactionManager.ExecuteTransactionAsync<AppSession, AuditLogSession>(
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
// ✓ 正しい: セッションを引数で受け取る
public sealed class UserAggregateService
{
    public ValueTask<User> GetRequiredAsync(
        AppSession session, UserId identifier, CancellationToken cancellationToken = default)
    { }
}
```

**なぜ:** トランザクションの境界はユースケースの粒度と一致していなければならず、
それを知っているのは `ICommandService` だけ。下の層が勝手に開始すると、
呼び出され方によって粒度が変わってしまう。

`ICommandService` が `ITransactionManager` を注入して `ExecuteTransactionAsync` で包み、
取り出したセッションを下の層へ渡す。**それより下の層はトランザクションを開始しない。**

ドメインサービスは `ExecuteAsync` にセッション引数が無いので、
**DTO にセッションを載せて渡す**（`IDomainServiceDTO` の実装がセッションを持ってよい唯一の理由）。

→ [use-case.md](use-case.md#コマンドサービスがトランザクションの境界)

---

## 6. リポジトリは `ObjectNotFoundException` を投げない

```csharp
// ✗ 間違い: リポジトリが不在を異常と決めつけている
public async ValueTask<Optional<User>> FindByIdentifierAsync(...)
{
    var row = await session.FindAsync(...);
    if (row is null)
    {
        throw new ObjectNotFoundException(typeof(User), identifier);
    }
}
```

```csharp
// ✓ 正しい: リポジトリは Empty を返し、
//           「無かったら失敗」を決めるのは集約サービス / ユースケース
public async ValueTask<User> GetRequiredAsync(
    AppSession session, UserId identifier, CancellationToken cancellationToken = default)
{
    var found = await GetEntityByIdentifierAsync(session, identifier, cancellationToken);
    if (!found.TryGetValue(out var user))
    {
        throw new ObjectNotFoundException(typeof(User), identifier);
    }

    return user;
}
```

**なぜ:** 「見つからない」が異常かどうかは操作次第。削除済みのものをもう一度削除するのは
問題ないが、無い口座から引き落とすのは問題。**それを知っているのは呼び出し側だけ。**

同じ理由で `SaveAsync` は `ObjectAlreadyExistException` も投げない（upsert なので、
既に居ることは失敗ではない）。「既に居たら失敗」も上位層で先に読んで判断する。

**例外は情報付きのコンストラクタを使う。** 対象型と識別子を渡すと、
呼び出し側でメッセージを組み立てなくてもログに出る。

```csharp
throw new ObjectNotFoundException(typeof(User), identifier);
```

→ [domain-model.md](domain-model.md#例外を投げる層)

---

## 7. `IRepository` に検索系メソッドを足さない

```csharp
// ✗ 間違い
public interface IUserRepository : IRepository<User, UserId, OperateInfo, AppSession>
{
    ValueTask<IReadOnlyList<User>> FindAllAsync(AppSession session, CancellationToken ct = default);
    ValueTask<IReadOnlyList<User>> FindByNameAsync(AppSession session, string name, CancellationToken ct = default);
}
```

```csharp
// ✓ 正しい: 一覧・検索はクエリサービスへ。ストアを直接読み、DTO を返す
public sealed class SearchUsersQueryService : IQueryService<SearchUsersReq, SearchUsersRes>
{
    public ValueTask<SearchUsersRes> ExecuteAsync(SearchUsersReq req, CancellationToken ct = default)
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
    // 検証はここだけ
    if (value.Length < MinLength || value.Length > MaxLength)
    {
        throw new ValueObjectLengthException(
            minLength: MinLength, maxLength: MaxLength, currentLength: value.Length);
    }

    return new UserName(value);
}

public static UserName Reconstruct(string value) => new(value);   // 検証しない
```

**なぜ:** `Reconstruct` は永続化からの復元用。**ルールを厳しくした後でも、
古いルールで保存されたデータを読み戻せるようにするため**に検証しない。
`Create` を通してしまうと、仕様変更のたびに既存データが読めなくなる。

- `Create` を呼ぶ: ユーザー入力、外部 API、その他信用できない入力
- `Reconstruct` を呼ぶ: リポジトリが永続化された値から復元するときだけ

**`Validate` メンバーは存在しない。** 外から呼べる検証があると
「未検証の値オブジェクトが存在しうる」ことになってしまうため v2.0.0 で削除された。
コンストラクタを private にして、`Create` / `Reconstruct` だけを入口にする。

→ [domain-model.md](domain-model.md#検証は-create-の中に書く)

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

## その他の細かい規約

### `ValueObjectLengthException` は名前付き引数で投げる

```csharp
// ✗ 順番を間違えてもコンパイルが通り、間違った境界値がログに出る
throw new ValueObjectLengthException(value.Length, MinLength, MaxLength);

// ✓
throw new ValueObjectLengthException(
    minLength: MinLength, maxLength: MaxLength, currentLength: value.Length);
```

引数が 3 つとも `int` なので型では守れない。順番は
`minLength` → `maxLength` → `currentLength`（宣言された上下限が先、弾かれた長さが最後）。

### レスポンス DTO にエンティティを入れない

エンティティは読み出したトランザクションのもので、
呼び出し側に届く頃にはセッションは `Dispose` 済み。DTO に詰め替える。

### DTO は `record` で宣言する

`ICommandServiceDTO` / `IQueryServiceDTO` / `IDomainServiceDTO` はいずれもマーカーで、
3 種のサービスが互いの DTO を受け取ってしまうのを防ぐためだけにある。

| DTO | セッションを持つか | エンティティを持つか |
|---|---|---|
| `ICommandServiceDTO` | **持たない**（自分で開始する） | 持たない |
| `IQueryServiceDTO` | 未コミットを読む必要があるときだけ | 持たない |
| `IDomainServiceDTO` | **持つ**（伝える経路が他に無い） | 持ってよい |

### `IQueryService<TRes>` の型引数はレスポンス

`ICommandService<TReq>` / `IDomainService<TReq>` の 1 つ目はリクエストだが、
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
