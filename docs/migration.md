# 移行ガイド

バージョンを上げるときに手を入れる箇所。
網羅的な変更点は [../CHANGELOG.md](../CHANGELOG.md) にあり、ここは**作業手順**に絞る。

| 移行 | 規模 | 主な作業 |
|---|---|---|
| [v2.x → v3.0.0](#v2x--v300) | **大** | `ITransactionService` 実装の修正、`Optional<T>` の生成方法、エンティティの等価性 |
| [v3.0.0 → v3.0.1](#v300--v301) | 小 | トランザクションの入れ子検出（`NestedTransactionException`）、`beforeRollbackHandler` の呼び出し位置（doc 訂正） |
| [v3.0.1 → v4.0.0](#v301--v400-validate-の復活と削除された型) | 中〜大 | `Validate` の復活、`ILengthDefinedSingleValueObject` / `IDomainService` 系 / 例外クラスの削除 |
| [v1.x → v2.0.x](#v1x--v20x) | 中 | 削除された型の置き換え、`Validate` の廃止、依存パッケージの明示 |

---

## v2.x → v3.0.0

破壊的変更が多いが、**大半はコンパイルエラーとして出る**ので順に潰せばよい。
コンパイルが通っても挙動が変わるものだけ、最後の「静かに変わるもの」にまとめてある。

### 1. `ITransactionService<TSession>` の実装を直す（必須）

`CancellationToken` が増えた。**さらに、セッションを `Dispose` してはいけなくなった。**

```csharp
// Before (v2)
public class AppTransactionService : ITransactionService<AppSession>
{
    public ValueTask<AppSession> BeginAsync() { }

    public ValueTask CommitAsync(AppSession session)
    {
        session.Commit();
        session.Dispose();      // ← v2 では実装側の責務だった
        return ValueTask.CompletedTask;
    }

    public ValueTask RollbackAsync(AppSession session)
    {
        session.Rollback();
        session.Dispose();
        return ValueTask.CompletedTask;
    }
}
```

```csharp
// After (v3)
public sealed class AppTransactionService : ITransactionService<AppSession>
{
    public ValueTask<AppSession> BeginAsync(CancellationToken cancellationToken = default) { }

    public ValueTask CommitAsync(AppSession session, CancellationToken cancellationToken = default)
    {
        session.Commit();
        return ValueTask.CompletedTask;   // Dispose しない
    }

    public ValueTask RollbackAsync(AppSession session, CancellationToken cancellationToken = default)
    {
        session.Rollback();
        return ValueTask.CompletedTask;   // Dispose しない
    }
}
```

**`Dispose` を残すと二重解放になる。** v3 では `ITransactionManager` が
commit 後・rollback 後・例外時のいずれの経路でも `Dispose` する
（v2 ではどの経路でも `Dispose` されておらず、接続リークしていた）。

非ジェネリックの `ITransactionService` が追加されたが、
`ITransactionService<TSession>` が明示的実装を提供するので**実装側の対応は不要**。

### 2. `ExecuteTransactionAsync` の本体に引数を足す（必須）

デリゲートが `Func<TransactionSessions, ValueTask>` から
`Func<TransactionSessions, CancellationToken, ValueTask>` になった。

```csharp
// Before
await _transactionManager.ExecuteTransactionAsync(
    ImmutableList.Create(typeof(AppSession)),
    async sessions => { /* ... */ });

// After — 型引数版が増えたので ImmutableList も要らなくなった
await _transactionManager.ExecuteTransactionAsync<AppSession>(
    async (sessions, token) => { /* ... */ },
    cancellationToken: cancellationToken);
```

型引数版（1〜3 個）は `IDisposable` でない型をコンパイルエラーにできるので、
`ImmutableList.Create(typeof(...))` 版より安全。4 個以上や実行時に決まる場合だけ
`ImmutableList<Type>` 版を使う。

### 3. `TransactionManager.Sessions` の利用を置き換える（該当すれば）

```csharp
// Before
var session = (AppSession)transactionManager.Sessions[typeof(AppSession)];

// After
var session = transactionManager.GetSession<AppSession>();
// または transactionManager.TryGetSession<AppSession>(out var session)
```

`TransactionSessions.Sessions` の型も
`IReadOnlyDictionary<Type, dynamic>` → `IReadOnlyDictionary<Type, IDisposable>` に変わった
（`src/` から `dynamic` を排除したため。AOT / trimming で効く）。

`GetSession(Type)` の戻り値も `object` → `IDisposable`。

### 4. `KeyNotFoundException` の捕捉を置き換える（該当すれば）

未開始のセッションを要求したときの例外が
`KeyNotFoundException` → `TransactionSessionNotFoundException` になった。

```csharp
catch (TransactionSessionNotFoundException exception)
{
    // exception.SessionType でどのセッション型か分かる
}
```

### 5. `Optional<T>` の生成方法を直す（該当すれば）

`readonly struct` になり、`HasValue` / `Value` の `init` アクセサが削除された。
オブジェクト初期化子では作れない。

```csharp
// Before
var optional = new Optional<int> { HasValue = true, Value = 5 };

// After — どれでもよい
var optional = Optional<int>.Some(5);
var optional = new Optional<int>(5);
var empty    = Optional<int>.Empty;
```

`HasValue = false` なのに値だけ入っている矛盾状態を作れなくするための変更。

`Optional(TValue? value, bool hasValue)` も、`hasValue` が false のときは
`value` を保持しなくなった（None は常に `default` を保持する）。

### 6. `OptionalExtensions.Exchange` を `Map` に置き換える

`[Obsolete]` になった（削除はされていない）。

```csharp
// Before
var mapped = optional.Exchange(user => user.Name);

// After
var mapped = optional.Map(user => user.Name);     // Select でも同じ
```

`Map` / `Select` / `Bind` / `SelectMany` / `Where` が揃ったので、
LINQ クエリ構文も使えるようになった。→ [optional.md](optional.md#変換連鎖する)

### 7. `innserException:` の綴りを直す（該当すれば）

例外クラス 7 個の内部例外パラメーター名の綴り誤りを修正した。

```csharp
// Before（コンパイルエラーになる）
throw new ObjectNotFoundException(innserException: exception);

// After
throw new ObjectNotFoundException(innerException: exception);
```

位置引数で渡している場合は影響しない。

### 8. `ITransactionManager` を自前実装している場合（該当すれば）

メンバーが増えたので実装の追加が必要。
`BeginTransactionAsync(Type)` / `BeginTransactionsAsync(ImmutableList<Type>)` /
`GetSession(Type)` / `TryGetSession<TSession>(out TSession)` /
`GetTransactionService(Type)` / `ExecuteTransactionAsync` のジェネリックオーバーロード。

`TransactionManager` をそのまま使っているなら影響はない。
なお `TransactionManager` は `sealed` になったので、継承していた場合は合成に変える。

### 静かに変わるもの（コンパイルは通る）

**ここだけは自分で探す必要がある。**

#### エンティティの等価性に実行時型が加わった

```csharp
// v2: Identifier が同じなら等価だった
// v3: 実行時型も一致しないと等価にならない
public class Admin : User { }   // User : EntityBase<User, UserId>
public class Guest : User { }

var admin = /* Identifier = X */;
var guest = /* Identifier = X */;

admin == guest;   // v2: true / v3: false
```

**バグ修正**だが、v2 の挙動に依存していたコードは壊れる。
`GetHashCode` も `HashCode.Combine(GetType(), Identifier)` になったので、
**永続化・キャッシュしたハッシュ値があれば作り直すこと。**

識別子の比較も `EqualityComparer<TIdentifier>.Default` を使うようになった。

#### `Optional<T>` の等価性が三状態を区別するようになった

`IEquatable<Optional<TValue>>` を実装し、`==` / `!=` / `Equals` / `GetHashCode` を定義した。
既定の `ValueType.Equals`（リフレクション比較）ではなくなっている。

- `None == None` → true
- `Some(null) == Some(null)` → true
- **`None == Some(null)` → false**

#### Mapster を使っている場合

`Optional<T>` が完全な不変型になったため、他の型から `Optional<T>` への
**規約ベースのマッピングが動かなくなる**（`Cannot convert immutable type` で例外になる。
値が黙って落ちることはない）。

```csharp
config.NewConfig<MPOptional<T>, Optional<T>>().MapWith(src => src.ToOptional());
```

`CSStack.TADA.MagicOnionHelper.Abstractions` の `MPOptional<T>` からの変換は、
パッケージが提供する `ToOptional()` / `FromOptional()` を使う。
`Optional<T>` → `MPOptional<T>` 方向は従来どおり規約マッピングで動く。

#### ロールバックの挙動が変わった（修正）

いずれもバグ修正なので、通常は良い方向にしか変わらない。

- 1 つのセッションのロールバックが失敗しても、残りが放置されなくなった
- commit の部分失敗時に、未 commit のセッションのロールバックを試みるようになった
  （**commit 済みの分は元に戻せない** — 2 相コミットではない）
- ロールバックは常にキャンセルされていないトークンで実行されるので、
  `CancellationToken` のキャンセルでトランザクションが開いたまま残らなくなった
- 失敗が複数あるときは `AggregateException` にまとめられる（最初の内部例外が元の失敗）
- `BeginTransactionsAsync` を直接呼んで途中で失敗した場合も、開始済みのセッションが
  ロールバック・`Dispose` されるようになった（放置されなくなった）

### 移行後の確認

- [ ] `ITransactionService` の実装から `Dispose` 呼び出しが消えている
- [ ] `TransactionManager` が **Scoped** で登録されている（v3 で変わったわけではないが、
      セッションを保持するようになったため事故ったときの被害が大きい）
- [ ] `ITransactionService<TSession>` がセッション型の数だけ登録されている
- [ ] エンティティの等価性に依存した処理を洗い出した
- [ ] 永続化・キャッシュされたハッシュ値を作り直した

---

## v3.0.0 → v3.0.1

コンパイルエラーになる変更は無い。**静かに壊れていたものが検出可能になる**変更が中心。
このほか、セッション型の扱い・サービスの口の立て方・DTO のネストに関する規約が
`docs/` に多数追加されたが、コードへの影響は無いので詳細は
[CHANGELOG.md の \[3.0.1\]](../CHANGELOG.md#301---2026-07-30) を参照。

### 1. トランザクションの入れ子が例外になった

v3.0.0 までは、実行中の `ExecuteTransactionAsync` の本体からもう一度
`ExecuteTransactionAsync` を呼べてしまい、**内側の commit が外側のセッションまで
確定して `Dispose` していた**。例外は出ず、外側の残りの処理は Dispose 済みセッションに
対して動き、外側の commit は「対象なし」で成功していた。

v3.0.1 からは入り口で `NestedTransactionException` を投げる。

```csharp
// v3.0.0 まで: 静かに壊れていた / v3.0.1: NestedTransactionException
await _transactionManager.ExecuteTransactionAsync<AppSession>(
    async (sessions, token) =>
    {
        await _otherCommandService.ExecuteAsync(dto, token);  // 中で ExecuteTransactionAsync を呼ぶ
    });
```

**直し方:** 共通処理をドメインサービス／集約サービスに切り出し、
同じ `ExecuteTransactionAsync` の本体からセッションを渡して呼ぶ。
連続して 2 つのトランザクションを張るのは従来どおり正当。

### 2. `beforeRollbackHandler` の呼び出し位置（doc の訂正）

挙動は変わっていないが、v3.0.1 で XML doc と `docs/` の記述を実態に合わせた。

- 本体（または begin）が失敗した場合 → ロールバック**前**に呼ばれる（セッションは生きている）
- **commit が失敗した場合 → ロールバック後**に呼ばれる。`CommitTransactionsAsync` が
  内部でロールバックと `Dispose` を済ませてから throw するため、
  **ハンドラから見えるセッションは Dispose 済み**

セッションを触る診断コードを書いている場合は、commit 失敗の経路を確認すること。

### 移行後の確認

- [ ] コマンドサービスから別のコマンドサービスを呼んでいる箇所を洗い出した
      （入れ子は `NestedTransactionException` になる）

---

## v3.0.1 → v4.0.0: Validate の復活と削除された型

**値オブジェクト・エンティティ・ドメインサービス・例外を自分で書いているプロジェクトは
ほぼ確実に影響を受ける。**

### 1. `IValueObject` / `IEntity<TIdentifier>` に `Validate()` を実装する（必須。コンパイルエラーになる）

`IValueObject` と `IEntity<TIdentifier>` に、既定実装の無い `void Validate();` が増えた。
これらを**直接実装している型はすべてコンパイルエラーになる。** `EntityBase<TSelf, TIdentifier>`
自身が `public abstract void Validate();` を宣言し直しているので、`EntityBase` を継承している
**エンティティも例外なく影響を受ける。**

```csharp
// Before（v4.0.0 のこの変更より前）
public sealed record UserName : ISingleValueObject<string, UserName>
{
    private UserName(string value) => Value = value;

    public string Value { get; }

    public static UserName Create(string value) { /* 検証 */ return new(value); }

    public static UserName Reconstruct(string value) => new(value);
}
```

```csharp
// After
public sealed record UserName : ISingleValueObject<string, UserName>
{
    private UserName(string value) => Value = value;

    public string Value { get; }

    public static UserName Create(string value)
    {
        CheckInvariants(value);
        return new UserName(value);
    }

    public static UserName Reconstruct(string value) => new(value);

    // 追加が必須。Create の代わりではなく、Create を通ったかどうかを外部から
    // 検証する術が無いために用意した独立のプリミティブ（Reconstruct 後の再チェックは一例）
    public void Validate() => CheckInvariants(Value);

    private static void CheckInvariants(string value) { /* Create と共有する検証 */ }
}
```

**`Validate()` は構築時の検証を置き換えない。** `Create` はそのままで、
`Create` と `Validate` の両方から呼べる `private` ヘルパー（`CheckInvariants` など）に
検証ロジックを集約するのが定石。用意した理由は、あるインスタンスが `Create` を通ったかどうかを
外部から検証する術が無いためで、`Validate()` 自体は何にも依存しない、今の値が現行の
不変条件を満たしているかを確認するだけのプリミティブ。エンティティは持っている値オブジェクトの
`Validate()` へ委譲すればよいことが多い（`samples/CSStack.TADA.Sample/Domain/User.cs` の
`Validate()` を参照）。戻り値は `void` 固定で、投げる例外の型はライブラリが指定しない。

→ [domain-model.md](domain-model.md#validate-は構築時の検証を置き換えない)

### 2. `ILengthDefinedSingleValueObject` を置き換える（該当すれば。コンパイルエラーになる）

型が削除されたので、実装している値オブジェクトと、
`where T : ILengthDefinedSingleValueObject` という制約を書いている箇所はコンパイルエラーになる。

```csharp
// Before
public sealed record UserName : ISingleValueObject<string, UserName>, ILengthDefinedSingleValueObject
{
    public static int MaxLength => 16;
    public static int MinLength => 1;
    // ...
}
```

```csharp
// After — interface を介さない素の static メンバーに変える。公開の仕方自体は変わらない
public sealed record UserName : ISingleValueObject<string, UserName>
{
    public static int MaxLength => 16;
    public static int MinLength => 1;
    // ...
}
```

ジェネリック制約で受けていた側は、制約を外して個別に `MaxLength` / `MinLength` を
参照する形に直す。

### 3. `IDomainService<TReq>` / `IDomainService<TReq, TRes>` / `IDomainServiceDTO` を置き換える（該当すれば。コンパイルエラーになる）

型が削除されたので、これらを継承・実装している箇所はコンパイルエラーになる。

```csharp
// Before
public interface IUserNameUniquenessService<TUserSession>
    : IDomainService<IUserNameUniquenessService<TUserSession>.Req>
    where TUserSession : IDisposable
{
    sealed record Req(TUserSession Session, UserName Name, UserId ExceptUserId)
        : IDomainServiceDTO;
}
```

```csharp
// After — 継承する型が無いので ExecuteAsync を自分で 1 つ宣言する。Req はただの record になる
public interface IUserNameUniquenessService<TUserSession>
    where TUserSession : IDisposable
{
    sealed record Req(TUserSession Session, UserName Name, UserId ExceptUserId);

    ValueTask ExecuteAsync(Req req, CancellationToken cancellationToken = default);
}
```

「専用の口を立て、リクエストをその中に `Req` としてネストする」という規約自体は変わらない。
→ [samples/CSStack.TADA.Sample/Domain/UserNameUniquenessService.cs](../samples/CSStack.TADA.Sample/Domain/UserNameUniquenessService.cs)

### 4. TADA 提供の例外クラスを自前の例外に置き換える（該当すれば。コンパイルエラーになる）

`DomainInvalidOperationException` / `ObjectAlreadyExistException` / `ObjectNotFoundException` /
`ValueObjectInvalidException` / `ValueObjectLengthException` / `ValueObjectNullException` が
削除された。これらを `throw` している箇所、`catch` している箇所はコンパイルエラーになる
（`TADAException` を基底として `catch` している箇所は、その基底自体は残っているので影響しない）。

```csharp
// Before
throw new ObjectNotFoundException(typeof(User), identifier);
throw new ValueObjectLengthException(
    minLength: MinLength, maxLength: MaxLength, currentLength: value.Length);
```

```csharp
// After — ドメインの語彙で自前に定義する
throw new UserNotFoundException(identifier.Value);
throw new UserNameLengthException(
    minLength: MinLength, maxLength: MaxLength, currentLength: value.Length);
```

単純な不変条件（空の `Guid` を拒否するなど）は、自前の例外を作らず `ArgumentException` の
ような BCL の例外で足りることもある。
→ [samples/CSStack.TADA.Sample/Domain/UserExceptions.cs](../samples/CSStack.TADA.Sample/Domain/UserExceptions.cs)

### 静かに変わるもの

**この 4 つに関しては無い。** いずれも「型が削除された」「既定実装の無いメンバーが増えた」
という直接的な変更なので、実際に使っていればコンパイルエラーとして必ず表面化する。
**逆にいえば、これらの型を一つも使っていないプロジェクトはこの節の影響を受けない。**

### 移行後の確認

- [ ] `IValueObject` / `IEntity<TIdentifier>` を直接実装している型、および
      `EntityBase<TSelf, TIdentifier>` を継承しているエンティティすべてに `Validate()` を実装した
- [ ] `ILengthDefinedSingleValueObject` を実装・制約に使っていた箇所を素の static メンバーに直した
- [ ] `IDomainService<TReq>` 系を継承していたドメインサービスの口を、
      自分で宣言した `ExecuteAsync` に直した
- [ ] TADA 提供の例外クラスを `throw` / `catch` していた箇所を自前の例外に置き換えた

---

## v1.x → v2.0.x

v2.0.0 / v2.0.1 / v2.0.2 は同日リリースで、PATCH 番号だが**内容は MAJOR 相当**。
まとめて移行するのが実際的。

### 1. 削除された型を置き換える

v1.1.0 で `[Obsolete]` になっていた型が v2.0.0 で削除された。

| 削除された型 | 移行先 |
|---|---|
| `ValueObjectBase` | `IValueObject` を直接実装する。**`record` で実装し、検証は `Create` の中に書く** |
| `ValidateHelper` | 例外を集約せず、`Create` にガード節を書く |
| `KeyedValidateHelper<TKey>` | 同上。UI 向けのエラー集約はアプリケーション層 / プレゼンテーション層で行う |
| `MultiReasonException` | 同上 |
| `KeyedMultiReasonException<TKey>` | 同上 |
| `IDomainServiceWithRes<TReq, TRes>` | `IDomainService<TReq, TRes>` |
| `ICommandServiceWithRes<TReq, TRes>` | `ICommandService<TReq, TRes>` |
| `IQueryServiceWithoutReq<TRes>` | `IQueryService<TRes>` |

下 3 つは**型名を差し替えるだけ**で移行できる（同名ジェネリック型のオーバーロードが用意された）。

### 2. `Validate()` をやめる（v2.0.1）

```csharp
// Before — 不正な値を保持したまま存在し、後から確かめる
var userName = new UserName(input);
if (userName.IsInvalidValue()) { /* ... */ }
```

```csharp
// After — 生成時に検証し、不正なインスタンスを作らせない
var userName = UserName.Create(input);   // 不正なら ValueObjectInvalidException（この型自体は v4.0.0 で削除。以後は自前の例外で代替）
```

削除されたもの:

- `IValueObject.Validate()` — `IValueObject` はメンバーの無いマーカーになった
- `IEntity<TIdentifier>.Validate()` と `IsInvalidValue`、および `EntityBase` の対応する実装
- `ValueObjectExtensions`（`IsInvalidValue` 拡張）— v2.0.0 で `ValueObjectBase.IsInvalidValue` の
  移行先として用意されたが、v2.0.1 で役目を終えた

**`Validate` メンバーはライブラリのどこにも存在しない。** 外から呼べる検証があると
「未検証の値オブジェクトが存在しうる」ことになってしまうため。
検証は `Create` の中に書き、永続化からの復元である `Reconstruct` では検証しない。

> **この方針は v4.0.0 で見直されました。** あるインスタンスが `Create` を通ったかどうかを
> 外部から検証する術が無いという不便が実用上多く、`Validate()` は
> [上のセクション](#v301--v400-validate-の復活と削除された型)で復活しています。
> 構築時の検証が `Create` の仕事であることは変わりません。

→ [domain-model.md](domain-model.md#検証は-create-の中に書く)

### 3. `Microsoft.Extensions.DependencyInjection.Abstractions` を明示する

v2.0.0 で**外部依存がゼロ**になった。`TransactionManager` が必要とするのは
`System.IServiceProvider` だけなので公開 API は変わらないが、
このパッケージが**推移的に流れてこなくなる。**

暗黙に依存していたプロジェクトはコンパイルエラーになるので、自分で追加する。

```
dotnet add package Microsoft.Extensions.DependencyInjection.Abstractions
```

### 4. `ITransactionManager` を自前実装している場合（v2.0.2）

インターフェースにメンバーが追加された（既定実装は無い）ため、コンパイルエラーになる。
`BeginTransactionAsync<TSession>()` / `CommitTransactionsAsync()` /
`RollbackTransactionsAsync()` / `GetSession<TSession>()` / `GetTransactionService<TSession>()`。

`TransactionManager` をそのまま使っていた場合は影響しない。

### あわせて使えるようになったもの

- `ISingleValueObject<TValue>` — `Value` だけを持つ型引数 1 個版。
  `static abstract` な `Create` / `Reconstruct` を要求しないので、
  ジェネリック制約に使える
- `OptionalExtensions` — `CreateSingleValueObject` / `ReconstructSingleValueObject` /
  `Exchange` / `ExchangeValueObjectToPrimitive`
  （`Exchange` は v3.0.0 で `[Obsolete]`。`Map` / `Select` を使う）

---

## v1.1.0 以前

CHANGELOG 導入前のため詳細な記録は無い。コミット履歴を参照。

v1.1.0（2026-06-04）で .NET 10 に対応し、v2.0.0 で削除する型を `[Obsolete]` にしている。
`ValueObjectBase` の等価性の欠陥（継承先が異なる値オブジェクトどうしが等価になる）は
この版で修正された。同じ問題のエンティティ側の対応は v3.0.0。

## 関連

- [../CHANGELOG.md](../CHANGELOG.md) — 変更点の網羅的な記録
- [best-practices.md](best-practices.md) — 移行後に守る規約
- [architecture.md](architecture.md) — なぜこの設計なのか
- [api-reference.md](api-reference.md) — 現在の公開型
