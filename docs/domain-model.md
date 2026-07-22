# ドメインモデルの規約

`Domain/` 配下の型が **何を強制し、何を強制しないか** をまとめます。
TADA が型で縛るのはごくわずかで、残りは規約です。強制していない部分こそ、
このページに書いてあるとおりに書いてください。

`Optional<T>` については [optional.md](optional.md)、
ユースケース層とトランザクションの接続については [use-case.md](use-case.md) を参照してください。

---

## 集約は「エンティティ 1・リポジトリ 1・集約サービス 1」

TADA が集約について強制することは 1 つだけです。

> **1 つの集約には、エンティティが 1 つ、そのリポジトリが 1 つ、集約サービスが 1 つ存在する。
> そして集約サービスからそのエンティティを取得できる。**

`IAggregateService<TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession>` の
型引数が 5 個あるのはこのためです。**書くのが面倒なぶんだけ、その集約の構成要素が
1 つずつであることが宣言されます。** 利便性のための基底クラスではないので、
`AggregateServiceBase` は `SaveAsync` や `DeleteAsync` のラッパーを持ちません。
リポジトリへの委譲だけを増やしても、層が 1 つ増えるだけで規約は増えないからです。

集約の操作は派生クラス側に書きます。`Repository` は `protected` で公開されています。

```csharp
public sealed class UserAggregateService
	: AggregateServiceBase<User, UserId, IUserRepository, OperateInfo, MySession>
{
	public UserAggregateService(IUserRepository repository)
		: base(repository)
	{
	}

	public async ValueTask ChangeNameAsync(
		MySession session,
		UserId userId,
		UserName newName,
		OperateInfo operateInfo,
		CancellationToken cancellationToken = default)
	{
		var optional = await GetEntityByIdentifierAsync(session, userId, cancellationToken);
		if (!optional.TryGetValue(out var user))
		{
			// 「見つからないことが問題か」を決めるのはこの層。リポジトリではない
			throw new ObjectNotFoundException($"User {userId.Value} was not found.");
		}

		await Repository.SaveAsync(session, user.ChangeName(newName), operateInfo, cancellationToken);
	}
}
```

---

## エンティティ

エンティティに必須なのは **識別子を持つこと**、そして **識別子で等価性を判断すること** の 2 つだけです。

```csharp
public class User : EntityBase<User, UserId>
{
	private User(UserId identifier, UserName name, DateTimeOffset registeredAt)
	{
		Identifier = identifier;
		Name = name;
		RegisteredAt = registeredAt;
	}

	public override UserId Identifier { get; }

	public UserName Name { get; }

	public DateTimeOffset RegisteredAt { get; }

	/// 新規作成。新しいエンティティとしての不変条件を適用する
	public static User Create(UserId identifier, UserName name, DateTimeOffset registeredAt)
	{
		return new User(identifier, name, registeredAt);
	}

	/// 永続化からの復元。検証済みの値なので不変条件を再適用しない
	public static User Reconstruct(UserId identifier, UserName name, DateTimeOffset registeredAt)
	{
		return new User(identifier, name, registeredAt);
	}

	public User ChangeName(UserName newName)
	{
		return new User(Identifier, newName, RegisteredAt);
	}
}
```

### 生成方法は強制しない

値オブジェクトと違い、エンティティに `Create` / `Reconstruct` の `static abstract` はありません。
**上の形を推奨しますが、縛りは設けていません。** コンストラクターで作っても構いません。

推奨する理由は値オブジェクトと同じで、「新規作成のときだけ適用したい不変条件」と
「保存済みデータの復元」を分けたいからです。ルールを後から厳しくしたとき、
`Create` しか無いと古いデータが読めなくなります。

### 等価性は「識別子が等しく、かつ実行時型が同じ」

`EntityBase<TSelf, TIdentifier>` は識別子だけで比較します。名前を変えても、
永続化から読み直しても、同じエンティティは同じエンティティです。

```csharp
var before = User.Reconstruct(userId, UserName.Create("変更前"), registeredAt);
var after = before.ChangeName(UserName.Create("変更後"));

before == after;   // true。識別子が同じなので同じエンティティ
```

**実行時型も一致していなければ等価になりません。** 同じ基底を継承した別種のエンティティが、
識別子の一致だけで等価になるのを防ぐためです。

```csharp
public class User : EntityBase<User, UserId> { /* ... */ }
public sealed class Admin : User { /* ... */ }
public sealed class Guest : User { /* ... */ }

User admin = /* Identifier = u1 */;
User guest = /* Identifier = u1 */;

admin == guest;    // false（v3.0.0 より前は true だった）
```

### 値の等価性がほしいならエンティティではない

内容で比較したいものは値オブジェクトです。`IValueObject` を実装した `record` にしてください。

---

## 値オブジェクト

### `record` で実装する

`IValueObject` は空のマーカーで、値の等価性を保証しません。**保証するのは `record` の役目です。**

```csharp
public sealed record Email : ISingleValueObject<string, Email> { /* ... */ }
```

`class` で実装すると参照の等価性が残り、同じ値を持つ 2 つのオブジェクトが等しくなりません。
これはマーカーインターフェースでは検出できない不具合です。`record` なら
値の等価性・`GetHashCode`・`ToString`・`with` がすべてコンパイラ生成で手に入ります。

かつて `ValueObjectBase` がありましたが、`record` のほうが素直なので v2.0.0 で削除しました。
**復活させません。**

### 検証は `Create` の中に書く

`IValueObject` / `IEntity` から `Validate` を外したのは意図的です。外から呼べる検証メソッドがあると
「未検証の値オブジェクトが存在しうる」ことになり、値オブジェクトの前提が崩れます。

**コンストラクターを `private` にして、`Create` と `Reconstruct` だけを入口にしてください。**
そうすれば「存在しているインスタンスは検証を通ったインスタンス」になります。

| メソッド | 検証 | 呼ぶ場所 |
|---|---|---|
| `Create` | **する** | 入力・API・他システムから来た値。要するに信用できない値すべて |
| `Reconstruct` | **しない** | リポジトリが永続化から復元するときだけ |

`Reconstruct` が検証しないのは手抜きではありません。**ルールを後から厳しくしたとき、
古い規則のもとで保存されたデータを読み戻せるようにするため**です。
`Create` はそれを拒否してしまいます。

検証に失敗したら `ValueObjectInvalidException` またはその派生を投げます。

| 例外 | 用途 |
|---|---|
| `ValueObjectInvalidException` | 不変条件違反全般。派生の基底 |
| `ValueObjectNullException` | 値が無い |
| `ValueObjectLengthException` | 長さが範囲外。`MinLength` / `MaxLength` / `CurrentLength` を持つ |

### 長さの制約は `ILengthDefinedSingleValueObject` で公開する

`ILengthDefinedSingleValueObject` は **境界値を公開するだけ** で、検証はしません。
検証するのは `Create` です。

```csharp
public sealed record UserName : ISingleValueObject<string, UserName>, ILengthDefinedSingleValueObject
{
	private UserName(string value)
	{
		Value = value;
	}

	public static int MaxLength => 32;

	public static int MinLength => 1;

	public string Value { get; }

	public static UserName Create(string value)
	{
		if (value is null)
		{
			throw new ValueObjectNullException($"{nameof(UserName)} must not be null.");
		}
		if (value.Length < MinLength || value.Length > MaxLength)
		{
			throw new ValueObjectLengthException(MinLength, MaxLength, value.Length);
		}

		return new UserName(value);
	}

	public static UserName Reconstruct(string value)
	{
		return new UserName(value);
	}
}
```

`ISingleValueObject` を継承しておらず型引数も持たないのは、
`where T : ILengthDefinedSingleValueObject` という制約だけで境界値に手が届くようにするためです。
プレゼンテーション層が `maxlength` をドメインと同じ数値から描画でき、
定数を 2 か所に書かずに済みます。

### 型引数が 1 個の `ISingleValueObject<TValue>`

`Create` / `Reconstruct` を含まない版です。「`TValue` を包む値オブジェクトなら何でも」を
ジェネリック制約で受けたいときに使います。**値オブジェクトを宣言するときは
`ISingleValueObject<TValue, TSelf>` のほうを使ってください。**

---

## リポジトリ

```csharp
public interface IUserRepository : IRepository<User, UserId, OperateInfo, MySession>
{
}
```

```csharp
public sealed class UserRepository : IUserRepository
{
	public async ValueTask<Optional<User>> FindByIdentifierAsync(
		MySession session,
		UserId identifier,
		CancellationToken cancellationToken = default)
	{
		var record = await session.Users.FindAsync(identifier.Value, cancellationToken);

		// 不在は Optional<User>.Empty。`return null;` と書くと Some(null) になる → optional.md
		return record is null
			? Optional<User>.Empty
			: User.Reconstruct(
				UserId.Reconstruct(record.Id),
				UserName.Reconstruct(record.Name),
				record.RegisteredAt);
	}

	public async ValueTask SaveAsync(
		MySession session,
		User entity,
		OperateInfo operateInfo,
		CancellationToken cancellationToken = default)
	{
		// upsert。存在すれば更新、なければ挿入
		await session.UpsertUserAsync(
			entity.Identifier.Value,
			entity.Name.Value,
			operateInfo.OperatorId,
			operateInfo.OperatedAt,
			cancellationToken);
	}
}
```

### `TOperateInfo` は「誰が・いつ」

書き込みと一緒に記録する操作情報です。操作者 ID・日時・リクエスト ID など、
システムに合わせて `record` で定義します。

```csharp
public sealed record OperateInfo(string OperatorId, DateTimeOffset OperatedAt);
```

書き込み系メソッドがすべてこれを受け取るのは、**保存処理では操作情報も一緒に保存するのが
ほとんどだから** です。引数にあれば、エンティティだけ保存して操作情報を書き忘れる、
という乖離が起きません。

**読み取りは受け取りません。** 読んでも何も変わらないので記録するものがありません。
読み取りを追跡したい場合はユースケース層で回収してください。リポジトリの
シグネチャを広げる必要はありません。

### `SaveAsync` は upsert

識別子に対応するレコードが無ければ挿入、あれば更新です。したがって
**「すでに存在する」「見つからない」で例外を投げません。** upsert なので、そもそも
それらは失敗ではありません。リポジトリから飛ぶ例外は接続断や制約違反といった
インフラ由来のもので、その場合はトランザクションがロールバックされます。

`SaveAsync` が書き込んだ内容が確定するのは **セッションが commit されたとき** であって、
`SaveAsync` から戻ったときではありません。

### 検索系メソッドを足さない

`IRepository` にあるのは `FindByIdentifierAsync` と `SaveAsync` だけです。
削除が必要なら `IRepositoryDeletable` を使います。**一覧取得・条件検索・ページングを
リポジトリに足さないでください。それらは `IQueryService` の仕事です**
（→ [use-case.md](use-case.md)）。

リポジトリは「書き込むために丸ごと復元する」ためのものです。エンティティは書き込みモデルであり、
一覧を作るために復元して平坦化するのは、無駄な組み立てを挟んだうえに
集約の境界を画面側へ引きずり出す行為になります。

---

## 例外を投げる層

**リポジトリは `ObjectNotFoundException` も `ObjectAlreadyExistException` も投げません。**

不在は `Optional<T>.Empty` で返ります。「見つからないこと」が問題かどうかは操作によって違うからです。
削除済みのものをもう一度削除するのは問題ないかもしれませんが、存在しない口座からの引き落としは問題です。
**この判断をするのは、そのオブジェクトを必要とした集約サービスかユースケース**です。

| 例外 | 投げる層 | 典型的な状況 |
|---|---|---|
| `ObjectNotFoundException` | 集約サービス / ユースケース | 対象が必須の操作で `Optional<T>.Empty` が返った |
| `ObjectAlreadyExistException` | 集約サービス / ユースケース | 一意であるべき対象が既に存在した（登録済みメールアドレス等） |
| `ValueObjectInvalidException`（派生含む） | 値オブジェクトの `Create` | 不変条件違反 |
| `DomainInvalidOperationException` | エンティティ / ドメインサービス | 状態的に許されない操作（退会済みユーザーの更新等） |

---

## ドメインサービス

**1 つのエンティティに閉じないルール** を置く場所です。
「このメールアドレスが他のユーザーに使われていないか」「口座 A から口座 B へ移す」など。

- 1 つのエンティティで完結するルール → **エンティティのメソッド**
- 1 つの集約で完結するルール → **集約サービス**
- 集約をまたぐルール → **ドメインサービス**
- 順序制御・認可・トランザクション → **コマンドサービス**（→ [use-case.md](use-case.md)）

`IDomainService<TReq>.ExecuteAsync` はセッションを引数に取りません。
**セッションはリクエスト DTO に載せてください。**

```csharp
public sealed record EnsureEmailIsUniqueRequest(MySession Session, Email Email) : IDomainServiceDTO;
```

ドメインサービスがトランザクションを開始することはありません。
`ITransactionManager` を注入しないでください。

---

## 早見表

| やりたいこと | 書き方 |
|---|---|
| エンティティを定義する | `EntityBase<TSelf, TIdentifier>` を継承し、識別子は値オブジェクトにする |
| 値オブジェクトを定義する | `sealed record` + `ISingleValueObject<TValue, TSelf>` |
| 値を検証する | `Create` の中で行い、`ValueObjectInvalidException` 派生を投げる |
| 永続化から復元する | `Reconstruct`（検証しない） |
| 長さの制約を持たせる | `ILengthDefinedSingleValueObject` で公開し、`Create` で検証する |
| 見つからなかったことを表す | `Optional<T>.Empty` を返す（`return null;` ではない） |
| 見つからないのを異常とみなす | 集約サービス / ユースケースで `ObjectNotFoundException` |
| 保存する | `SaveAsync`（upsert）。操作情報を必ず渡す |
| 一覧・条件検索をする | `IQueryService`。リポジトリには足さない |
| 集約をまたぐルールを書く | ドメインサービス。セッションはリクエスト DTO に載せる |

## 関連

- [optional.md](optional.md) — `Optional<T>` の三状態と `return null` の罠
- [use-case.md](use-case.md) — コマンド / クエリ / ドメインサービスの使い分けとトランザクション
- `tests/CSStack.TADA.Tests/EntityBaseTests.cs` — エンティティの等価性を固定したテスト
