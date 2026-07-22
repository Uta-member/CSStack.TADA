# `Optional<T>` — 三状態の値

`Optional<TValue>` は「値が設定されているか」を表す `readonly struct` です。
`IRepository.FindByIdentifierAsync` の戻り値でもあるため、**このライブラリで最初に触る型**になります。

`Nullable<T>` の代替ではありません。**三状態**を表現します。

| 状態 | 意味 | 生成 | `HasValue` | `Value` |
|---|---|---|---|---|
| `None` | 値が設定されていない | `Optional<T>.Empty` / `default` / `new Optional<T>()` | `false` | `default` |
| `Some(null)` | 値として null が設定されている | `Optional<T?>.Some(null)` | `true` | `null` |
| `Some(value)` | 値が設定されている | `Optional<T>.Some(value)` / 暗黙変換 | `true` | その値 |

---

## なぜ三状態なのか

**「未指定」と「null を指定した」を区別するため**です。典型例は HTTP PATCH の部分更新です。

```jsonc
// リクエストA: ニックネームを消したい
{ "nickName": null }

// リクエストB: ニックネームには触れず、メールアドレスだけ変えたい
{ "email": "new@example.com" }
```

`string?` で受けるとどちらも `null` になり区別できません。`Optional<string?>` なら
A は `Some(null)`、B は `None` になります。

```csharp
public sealed record UpdateUserCommand
{
	public required UserId UserId { get; init; }

	// 未指定なら None、null 指定なら Some(null)、値指定なら Some(value)
	public Optional<string?> NickName { get; init; }
	public Optional<string> Email { get; init; }
}
```

```csharp
// 適用側。None のときは何もしない = 既存値を維持する
command.NickName.Match(
	onSome: nickName => user.ChangeNickName(nickName),   // null なら「消す」
	onNone: () => { });                                   // 未指定なら触らない
```

「設定されているか否か」だけが必要で null を値として扱わないなら、
`Optional<T>` は実質 `Nullable<T>` と同じ挙動になります。それでも構いません。

---

## 最大の罠: `return null` は `None` ではない

`TValue` からの暗黙変換があるため、`Optional<T>` を返すメソッドで `return null;` と書くと
`Optional<T>.Empty` ではなく **`Some(null)` になります**。

```csharp
// ❌ 間違い。見つからなかったつもりが Some(null) になる
public async ValueTask<Optional<User>> FindByIdentifierAsync(
	MySession session,
	UserId identifier,
	CancellationToken cancellationToken = default)
{
	var record = await session.Users.FindAsync(identifier.Value, cancellationToken);
	if (record is null)
	{
		return null;   // → HasValue = true, Value = null
	}

	return User.Reconstruct(record);
}
```

呼び出し側はこうなります。

```csharp
if (optional.TryGetValue(out var user))   // true が返る
{
	user.DoSomething();                    // NullReferenceException
}
```

```csharp
// ✅ 正しい。不在は Optional<T>.Empty
public async ValueTask<Optional<User>> FindByIdentifierAsync(
	MySession session,
	UserId identifier,
	CancellationToken cancellationToken = default)
{
	var record = await session.Users.FindAsync(identifier.Value, cancellationToken);
	if (record is null)
	{
		return Optional<User>.Empty;
	}

	return User.Reconstruct(record);
}
```

**リポジトリで「見つからなかった」を表すのは常に `Optional<T>.Empty` です。**
`null` を返さないこと、そして `ObjectNotFoundException` を投げないこと
（見つからないのは正常な結果であり、例外にするかどうかは呼び出し側が決めます）。

---

## 型引数の null 許容性を正しく宣言する

**この罠をコンパイラに検出させられるかどうかは、型引数の宣言だけで決まります。**

- null が値として正当 → `Optional<string?>`
- null が正当でない → `Optional<User>` / `Optional<string>`

```csharp
// Optional<User> は「null にはならない値」の宣言なので…
Optional<User> Find() => null;      // CS8625: null リテラルを非 null 許容型に変換できません ✅

// Optional<string?> は null が値として正当なので、逆参照が警告される
Optional<string?> optional = GetNickName();
if (optional.TryGetValue(out var nickName))
{
	_ = nickName.Length;             // CS8602: null 参照の可能性 ✅
}
```

逆に `Optional<User>`（非 null 許容）に対して `Some(null)` を作ってしまった場合、
それは**型引数の宣言に対する契約違反**なので、逆参照側では警告が出ません。
警告を出す責任は値を作る側にあり、そこは上の CS8625 で塞がっています。

`TryGetValue` の `out` には `[MaybeNullWhen(false)]` が付いているため、
**false が返ったあとに `out` の値を使うと警告されます**。

```csharp
if (!optional.TryGetValue(out var user))
{
	_ = user.Identifier;             // CS8602 ✅
}
```

---

## 生成する

```csharp
Optional<User>.Empty                      // None
default(Optional<User>)                   // None（Empty と等価）
new Optional<User>()                      // None（Empty と等価）
new Optional<User>(value, hasValue: false) // None。value は捨てられる

Optional<User>.Some(user)                 // Some(user)
new Optional<User>(user)                  // Some(user)
Optional<string?>.Some(null)              // Some(null)（明示的に書く）

Optional<User> optional = user;           // 暗黙変換 → Some(user)
```

`init` アクセサはありません（v3.0.0 で削除）。`HasValue = false` なのに値だけ入っている、
という矛盾した状態は作れなくなっています。

> **Mapster を使う場合**: `Optional<T>` は完全な不変型なので、他の型から `Optional<T>` への
> 規約ベースのマッピングは `Cannot convert immutable type` で失敗します（値が黙って落ちることはありません）。
> `MapWith` を登録してください。
>
> ```csharp
> config.NewConfig<MPOptional<User>, Optional<User>>().MapWith(src => src.ToOptional());
> ```
>
> `Optional<T>` → 他の型の方向は従来どおり規約マッピングで動きます。

## 取り出す

```csharp
// 三状態をすべて区別する
if (optional.HasValue)
{
	// Some(value) または Some(null)
}

// 推奨: TryGetValue
if (optional.TryGetValue(out var user))
{
	// Some。Optional<T?> の場合 user は null かもしれない
}

// 網羅的に分岐する
var displayName = optional.Match(
	onSome: user => user.Name,
	onNone: () => "(未登録)");

// 既定値つきで取り出す
var name = optional.GetValueOrDefault(User.Guest);   // GetValue と同じ
```

`Value` プロパティは `None` でも `Some(null)` でも null / `default` を返すため、
**`Value` だけでは二つを区別できません。** `HasValue` か `TryGetValue` を使ってください。

`GetValue` / `GetValueOrDefault` は `Some(null)` に対して**既定値ではなく null を返します**。
`Some(null)` は「値が設定されている」状態だからです。

---

## 変換・連鎖する

| メソッド | 用途 | `None` のとき |
|---|---|---|
| `Map(f)` / `Select(f)` | 中の値を別の型に変換する | デリゲートを呼ばず `None` |
| `Bind(f)` / `SelectMany(f)` | `Optional<T>` を返す関数を連鎖する | 同上 |
| `Where(pred)` | 条件を満たさなければ `None` にする | 同上 |
| `Match(onSome, onNone)` | 両方の場合を網羅して畳み込む | `onNone` を呼ぶ |

```csharp
// Map: Some の状態は保たれる（f が null を返せば Some(null) になる）
Optional<string> name = userOptional.Map(user => user.Name);

// Bind: Some から None に落とせる。失敗しうる検索の連鎖に使う
Optional<Address> address = userOptional.Bind(user => FindAddress(user.Identifier));

// Where: 条件で絞る
Optional<User> active = userOptional.Where(user => user.IsActive);
```

`Select` / `SelectMany` / `Where` が揃っているので、**LINQ クエリ構文が使えます**。

```csharp
var postalCode =
	from user in FindUser(userId)
	from address in FindAddress(user.Identifier)
	where address.IsPrimary
	select address.PostalCode;
// → Optional<PostalCode>。どこかが None なら結果も None
```

> **注意**: `Some(null)` に対しては `Map` / `Where` / `Match(onSome)` のデリゲートが
> **null を引数に呼ばれます**。`Optional<T?>` を扱うときはデリゲート内で null を考慮してください。

値オブジェクト向けのショートカットもあります。

```csharp
Optional<UserName> vo = nameOptional.CreateSingleValueObject<string, UserName>();      // Create（検証あり）
Optional<UserName> vo2 = nameOptional.ReconstructSingleValueObject<string, UserName>(); // Reconstruct（検証なし）
Optional<string> raw = vo.ExchangeValueObjectToPrimitive<UserName, string>();
```

> `Exchange` は `Map` にリネームされ、`[Obsolete]` として残っています。新規コードでは `Map` を使ってください。

---

## 比較する

`IEquatable<Optional<TValue>>` を実装しており、`==` / `!=` / `Equals` / `GetHashCode` が定義済みです。
**三状態は等価性でも区別されます。**

```csharp
Optional<string?>.Empty      == Optional<string?>.Empty       // true
Optional<string?>.Some(null) == Optional<string?>.Some(null)  // true
Optional<string?>.Empty      == Optional<string?>.Some(null)  // false ← 三状態設計の要
Optional<string>.Some("a")   == Optional<string>.Some("a")    // true
```

中の値の比較には `EqualityComparer<TValue?>.Default` を使います。
`ToString()` は `"None"` / `"Some(null)"` / `"Some(値)"` を返します。

---

## 早見表

| やりたいこと | 書き方 |
|---|---|
| 見つからなかった | `return Optional<T>.Empty;`（`return null;` ではない） |
| null を値として設定した | `Optional<T?>.Some(null)` |
| 値の有無を調べる | `HasValue` / `TryGetValue` |
| 未指定なら何もしない | `optional.Match(onSome: ..., onNone: () => { })` |
| 中の値を変換する | `Map` / `Select` |
| 失敗しうる検索を連鎖する | `Bind` / `SelectMany` |
| null が正当な値 | `Optional<string?>` と宣言する |
| null が正当でない | `Optional<User>` と宣言する |

## 関連

- [CHANGELOG.md](../CHANGELOG.md) — v3.0.0 での破壊的変更
- `tests/CSStack.TADA.Tests/OptionalTests.cs` — 三状態の振る舞いを固定したテスト。実例として読めます
