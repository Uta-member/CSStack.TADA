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

### インターフェースを立ててから実装する

**`IAggregateService` を継承した集約サービスのインターフェースを宣言し、
その実装として `AggregateServiceBase` の派生クラスを書いてください。**
上の層（ユースケース）が注入するのはインターフェースです。

```csharp
public interface IUserAggregateService<TSession>
	: IAggregateService<User, UserId, IUserRepository<TSession>, OperateInfo, TSession>
	where TSession : IDisposable
{
	ValueTask ChangeNameAsync(
		TSession session,
		UserId userId,
		UserName newName,
		OperateInfo operateInfo,
		CancellationToken cancellationToken = default);
}
```

`AggregateServiceBase` の派生クラスを直接注入すると、**ユースケースのテストのために
集約サービスの具象クラスを組み立てることになり、その先のリポジトリ実装まで必要になります。**
基底クラスは実装の詳細であって、上の層に見せる契約ではありません。

集約の操作は派生クラス側に書きます。`Repository` は `protected` で公開されています。

```csharp
public sealed class UserAggregateService<TSession>
	: AggregateServiceBase<User, UserId, IUserRepository<TSession>, OperateInfo, TSession>,
	IUserAggregateService<TSession>
	where TSession : IDisposable
{
	public UserAggregateService(IUserRepository<TSession> repository)
		: base(repository)
	{
	}

	public async ValueTask ChangeNameAsync(
		TSession session,
		UserId userId,
		UserName newName,
		OperateInfo operateInfo,
		CancellationToken cancellationToken = default)
	{
		var optional = await GetEntityByIdentifierAsync(session, userId, cancellationToken);
		if (!optional.TryGetValue(out var user))
		{
			// 「見つからないことが問題か」を決めるのはこの層。リポジトリではない
			// UserNotFoundException はこのプロジェクトが自分で定義する例外（TADA は提供しない）
			throw new UserNotFoundException(userId.Value);
		}

		await Repository.SaveAsync(session, user.ChangeName(newName), operateInfo, cancellationToken);
	}
}
```

**セッション型は型引数として受け取り、具体型を書きません。** 集約サービスまでは扱う
リポジトリが 1 つなので、名前は素の `TSession` で構いません。集約をまたぐ層
（ドメインサービス・ユースケース）では `TUserSession` のように集約名を入れます
（→ [architecture.md](architecture.md#ドメイン層に具体的なセッション型を書かない)）。

### 口に並べるのはドメインの操作だけ。`SaveAsync` を置かない

**この口は実質的に集約ルートで、並ぶメソッドが「この集約に何ができるか」の一覧になります。**
`ChangeNameAsync` のように名前がドメインの語彙になっているのはそのためで、
ここに汎用の `SaveAsync` を足してはいけません。

```csharp
// ✗ RegisterAsync と並べると、呼ぶ側は何でも通る SaveAsync を選ぶ。
//    RegisterAsync に置いた「既に居たら失敗」は素通りされ、upsert で黙って上書きされる
ValueTask SaveAsync(TSession s, User u, OperateInfo o, CancellationToken ct = default);
```

上の `ChangeNameAsync` のように、**「取得する → 変更する → 保存する」を 1 つの操作に閉じます。**
そうすればエンティティを集約の外へ出さずに済みます。逆にエンティティを返してしまうと、
上の層で書き換えても保存する手段が口に無く、「変更したつもりが何も起きない」コードが書けます。
不在を例外にするヘルパー（`GetRequiredAsync` など）は便利ですが、**`private` に留めてください。**
読み取りが目的なら、それはクエリサービスの仕事です。

リポジトリの `SaveAsync` を呼ぶのはこの層までで、ユースケースからは呼びません。

DI 登録も「インターフェース → 実装」の形になります。

```csharp
services.AddScoped<IUserAggregateService<AppSession>, UserAggregateService<AppSession>>();
```

---

## エンティティ

エンティティに必須なのは **識別子を持つこと**、**識別子で等価性を判断すること**、
そして **`Validate()` を実装すること** の 3 つです。

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

	/// 不変条件を確かめる。破っていれば例外を投げる。持っている値オブジェクトへ委譲すればよいことが多い
	public override void Validate()
	{
		Identifier.Validate();
		Name.Validate();
	}
}
```

### `Validate()` は構築時の検証を置き換えない

`IEntity<TIdentifier>` は `Validate()` を要求します（既定実装は無いので、実装は必須）。
`EntityBase<TSelf, TIdentifier>` はこれを `public abstract void Validate();` として宣言し直しているので、
派生クラスは必ず実装します。

**構築時の検証は引き続き `Create` の仕事です。** `Validate()` はそれを置き換えるものではなく、
`Reconstruct` で古いルールのデータを復元した後などに、任意のタイミングで不変条件を
再チェックするための別経路です。戻り値は `void` 固定で、破っていれば例外を投げる運用にします
（どの例外を投げるかはライブラリが指定しません → [例外を投げる層](#例外を投げる層)）。

エンティティの検証は、上の `User.Validate()` のように**持っている値オブジェクトの検証へ委譲する**
だけで済むことが多いです。

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

### 検証は `Create` の中に書く。`Validate()` は別経路

**コンストラクターを `private` にして、`Create` と `Reconstruct` だけを入口にしてください。**
そうすれば「存在しているインスタンスは検証を通ったインスタンス」になります。

| メソッド | 検証 | 呼ぶ場所 |
|---|---|---|
| `Create` | **する** | 入力・API・他システムから来た値。要するに信用できない値すべて |
| `Reconstruct` | **しない** | リポジトリが永続化から復元するときだけ |

`Reconstruct` が検証しないのは手抜きではありません。**ルールを後から厳しくしたとき、
古い規則のもとで保存されたデータを読み戻せるようにするため**です。
`Create` はそれを拒否してしまいます。

`IValueObject` は `Validate()` を要求します（既定実装は無いので、実装は必須です）。
**これは `Create` の代わりではありません。** `Create` を通った時点で検証済みなので、
`Create` の中から `Validate()` を呼ぶ必要はなく、単に同じチェックを繰り返すだけになります。
用意した理由は、あるインスタンスが `Create` を通ったかどうかを外部から検証する術が
無いことです。`Reconstruct` で古いルールのデータを復元した後に使うのはその一例に
過ぎません。`Validate()` 自体は何かに依存する概念ではなく、単に今の値が現行の不変条件を
満たしているかどうかを確認するだけのプリミティブなメソッドです。

**`Create` と `Validate` の検証は同じ `private` ヘルパーに集約してください。**

```csharp
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

	public void Validate() => CheckInvariants(Value);

	private static void CheckInvariants(string value)
	{
		if (value is null)
		{
			throw new UserNameInvalidException($"{nameof(UserName)} must not be null.");
		}
	}
}
```

検証に失敗したときに何を投げるかは、**このライブラリではなくプロジェクト自身が決めます。**
TADA は値オブジェクト用の例外クラスを提供しません。`Create` / `Validate` の失敗を
`UserNameInvalidException` のような自前の例外にするか、単純な不変条件なら
`ArgumentException` のような BCL の例外で足りることもあります
（→ [例外を投げる層](#例外を投げる層)）。

### 長さの制約は素の static メンバーで公開する

長さの上下限を公開したい値オブジェクトは、interface を介さず
`public static int MaxLength => ...;` / `public static int MinLength => ...;` を
**素の static メンバーとして**宣言するだけです（`ILengthDefinedSingleValueObject` は
v3.0.0 で削除されました。境界値を公開するためだけの効果しかなく、
ジェネリック制約以外の用途がありませんでした）。**公開するだけで、強制はしません。**
強制するのは `Create` です。

```csharp
public sealed record UserName : ISingleValueObject<string, UserName>
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
		CheckInvariants(value);
		return new UserName(value);
	}

	public static UserName Reconstruct(string value)
	{
		return new UserName(value);
	}

	public void Validate() => CheckInvariants(Value);

	private static void CheckInvariants(string value)
	{
		if (value is null)
		{
			throw new UserNameInvalidException($"{nameof(UserName)} must not be null.");
		}
		if (value.Length < MinLength || value.Length > MaxLength)
		{
			throw new UserNameLengthException(
				minLength: MinLength,
				maxLength: MaxLength,
				currentLength: value.Length);
		}
	}
}
```

`UserNameLengthException` の引数は **3 つとも `int`** にするのが定石です。順序を間違えても
コンパイルが通り、誤った内容の例外になります。順序は
`minLength`, `maxLength`, `currentLength`（**値オブジェクトが宣言する境界が先、
弾かれた値の長さが最後**）で、迷うなら名前付き引数で書いてください。

プレゼンテーション層は `UserName.MaxLength` をそのまま入力欄の `maxlength` に使えるので、
同じ数字を 2 か所に書かずに済みます。

### 型引数が 1 個の `ISingleValueObject<TValue>`

`Create` / `Reconstruct` を含まない版です。「`TValue` を包む値オブジェクトなら何でも」を
ジェネリック制約で受けたいときに使います。**値オブジェクトを宣言するときは
`ISingleValueObject<TValue, TSelf>` のほうを使ってください。**

---

## リポジトリ

インターフェースはドメイン層に置き、**セッション型は型引数のまま開いておきます。**

```csharp
public interface IUserRepository<TSession> : IRepository<User, UserId, OperateInfo, TSession>
	where TSession : IDisposable
{
}
```

実装はインフラ層です。**具体的なセッション型を名指しするのはこちらだけ**で、
ここで型引数を閉じます。

```csharp
public sealed class UserRepository : IUserRepository<MySession>
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

### `TSession` は型引数のまま開く

```csharp
// ✗ ドメイン層のインターフェースがインフラ層の実トランザクション因子を名指ししている
public interface IUserRepository : IRepository<User, UserId, OperateInfo, MySession>;

// ✓ 具体型は実装（インフラ層）と DI 登録（プレゼンテーション層）だけが知っている
public interface IUserRepository<TSession> : IRepository<User, UserId, OperateInfo, TSession>
	where TSession : IDisposable;
```

前者もコンパイルは通りますが、ドメイン層が特定のデータストア実装に張り付き、
依存性逆転もテストダブルも成立しなくなります。**`TSession` を引き回すというのは
「型引数を引き回す」ことであって、「具体型をドメインに焼き込む」ことではありません**
（→ [architecture.md](architecture.md#ドメイン層に具体的なセッション型を書かない)）。

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

**TADA はドメイン向けの例外クラスを提供しません。** ライブラリ自身が投げるのは
`NestedTransactionException` と `TransactionSessionNotFoundException`（と基底の `TADAException`）
だけで、それ以外の例外はプロジェクトが自分のドメインの語彙で定義します
→ [samples/CSStack.TADA.Sample/Domain/UserExceptions.cs](../samples/CSStack.TADA.Sample/Domain/UserExceptions.cs)。

**リポジトリは「見つからない」も「既に存在する」も例外にしません。**

不在は `Optional<T>.Empty` で返ります。「見つからないこと」が問題かどうかは操作によって違うからです。
削除済みのものをもう一度削除するのは問題ないかもしれませんが、存在しない口座からの引き落としは問題です。
**この判断をするのは、そのオブジェクトを必要とした集約サービスかユースケース**です。

| 状況 | 投げる層 | 例（プロジェクトが定義する例外） |
|---|---|---|
| 対象が必須の操作で `Optional<T>.Empty` が返った | 集約サービス / ユースケース | `UserNotFoundException` |
| 一意であるべき対象が既に存在した（登録済みメールアドレス等） | 集約サービス / ユースケース | `UserAlreadyExistsException` |
| 値オブジェクトの不変条件違反 | 値オブジェクトの `Create` | `UserNameInvalidException`。単純な不変条件なら `ArgumentException` で足りることもある |
| 状態的に許されない操作（退会済みユーザーの更新等） | エンティティ / ドメインサービス | `UserSuspendedException` |

### 対象の型と識別子をメッセージに残す

TADA は対象の型と識別子を保持する例外クラスをもう提供しませんが、**同じ考え方は自分で
定義する例外にも持たせられます。** 対象を特定する情報をプロパティとして保持しておけば、
呼び出し側でメッセージを組み立てなくてもログから「どの型の、どれが」問題だったのかを追えます。

```csharp
public sealed class UserNotFoundException : Exception
{
	public UserNotFoundException(Guid userId)
		: base($"ユーザー '{userId}' が見つかりません。")
	{
		UserId = userId;
	}

	public Guid UserId { get; }
}
```

一意性違反の例外（`UserAlreadyExistsException` など）についても同様で、
**主キーよりも「一意であるべき値」**を持たせることが多くなります（重複したメールアドレスなど）。
メッセージに埋め込む値には、**秘密の値を渡さないでください。**

---

## ドメインサービス

**1 つのエンティティに閉じないルール** を置く場所です。
「このメールアドレスが他のユーザーに使われていないか」「口座 A から口座 B へ移す」など。

- 1 つのエンティティで完結するルール → **エンティティのメソッド**
- 1 つの集約で完結するルール → **集約サービス**
- 集約をまたぐルール → **ドメインサービス**
- 順序制御・認可・トランザクション → **コマンドサービス**（→ [use-case.md](use-case.md)）

**TADA にドメインサービス用の共通インターフェースはありません。** 集約サービスやユースケースと
違い、ドメインサービスは扱う対象・引数・戻り値の形がプロジェクトごとに柔軟すぎて、
共通の親インターフェースを立てても「メソッド名と Req/Res の形を強制するだけ」の効果しかなく、
実際に使う場面がほとんど無かったため、`IDomainService<TReq>` / `IDomainService<TReq, TRes>` /
`IDomainServiceDTO` は v3.0.0 で削除されました。

**それでも「専用の口を立て、リクエストをその中に `Req` としてネストする」という規約自体は
他の 3 種のサービスと変わりません。** `ExecuteAsync` を自分で 1 つ宣言するだけです。
セッションを渡す引数はありません。**セッションはリクエスト DTO に載せてください。**

```csharp
public interface IEmailUniquenessService<TUserSession>
	where TUserSession : IDisposable
{
	sealed record Req(TUserSession Session, Email Email);

	ValueTask ExecuteAsync(Req req, CancellationToken cancellationToken = default);
}

public sealed class EmailUniquenessService<TUserSession> : IEmailUniquenessService<TUserSession>
	where TUserSession : IDisposable
{
	// ...
}
```

→ [samples/CSStack.TADA.Sample/Domain/UserNameUniquenessService.cs](../samples/CSStack.TADA.Sample/Domain/UserNameUniquenessService.cs)
が動く実例です（`IUserNameUniquenessService<TUserSession>` は何も継承せず、
`Req` はどのマーカーインターフェースも実装しません）。

注入するだけなら口は要りません。それでも口を立てるのは、**リクエストの置き場所にするためです。**
DTO を名前空間に平らに置くと、「このドメインサービスに何を渡すのか」を名前で探すことになり、
口と DTO の対応が誰にも保証されません
（→ [use-case.md](use-case.md#リクエストとレスポンスは口の中にネストする)）。

**DTO に載せるセッションも具体型ではなく型引数です**（ネストしていれば口の型引数がそのまま使えます）。
そしてドメインサービスは集約をまたぐので、型引数の名前は `TSession` ではなく
`T[集約名]Session` にします。触る集約が増えたら
`IEmailUniquenessService<TUserSession, TInvitationSession>` のように並べられるからです
（→ [architecture.md](architecture.md#型引数の名前-集約をまたぐ層では-t集約名session)）。

ドメインサービスがトランザクションを開始することはありません。
`ITransactionManager` を注入しないでください。

---

## 早見表

| やりたいこと | 書き方 |
|---|---|
| エンティティを定義する | `EntityBase<TSelf, TIdentifier>` を継承し、識別子は値オブジェクトにする |
| 値オブジェクトを定義する | `sealed record` + `ISingleValueObject<TValue, TSelf>` |
| 値を検証する | `Create` の中で行い、自前の例外（または `ArgumentException`）を投げる |
| 永続化から復元する | `Reconstruct`（検証しない） |
| 不変条件を再チェックする | `Validate()`。`Create` の代わりではなく別経路 |
| 長さの制約を持たせる | 素の `static int MaxLength` / `MinLength` を公開し、`Create` で検証する |
| 見つからなかったことを表す | `Optional<T>.Empty` を返す（`return null;` ではない） |
| 見つからないのを異常とみなす | 集約サービス / ユースケースで自前の `NotFoundException` を投げる |
| 保存する | 集約サービスの中から `Repository.SaveAsync`（upsert）。操作情報を必ず渡す |
| 一覧・条件検索をする | `IQueryService`。リポジトリには足さない |
| 集約をまたぐルールを書く | ドメインサービス。口を立て、セッションを載せた `Req` をその中にネストする |
| 集約サービスを定義する | `IAggregateService` を継承した口を宣言し、`AggregateServiceBase` の派生で実装する |
| 集約サービスに操作を足す | ドメインの操作の名前で。`SaveAsync` のような汎用名は置かない |
| 集約サービスを注入する | 具象クラスではなくインターフェース（`IUserAggregateService<TSession>`） |
| セッション型を受け取る | 型引数で受ける。集約サービスまでは `TSession`、集約をまたぐなら `T[集約名]Session` |
| セッションの具体型を決める | ドメイン層では決めない。実装（インフラ層）と DI 登録（プレゼンテーション層） |

## 関連

- [optional.md](optional.md) — `Optional<T>` の三状態と `return null` の罠
- [use-case.md](use-case.md) — コマンド / クエリ / ドメインサービスの使い分けとトランザクション
- `tests/CSStack.TADA.Tests/EntityBaseTests.cs` — エンティティの等価性を固定したテスト
