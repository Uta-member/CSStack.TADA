# Changelog

このファイルは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に沿って記述し、
バージョンは [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## バージョニング方針

- バージョンは [Semantic Versioning 2.0.0](https://semver.org/lang/ja/) に従います。
  - **MAJOR**: 公開 API の破壊的変更。型やメンバーの削除・改名・シグネチャ変更に加えて、
    **既定実装のないメンバーをインターフェースに追加すること**も含みます。
    このライブラリはインターフェース中心で、利用者が `ITransactionManager` や
    `ITransactionService<TSession>` を自前で実装するため、メンバーの追加でも実装側が壊れるからです。
  - **MINOR**: 後方互換を保った機能追加。
  - **PATCH**: 後方互換を保ったバグ修正・ドキュメント修正。
- 型を削除するときは、いきなり消さず `[Obsolete]` を経てから次の MAJOR で削除します
  （v1.1.0 で非推奨 → v2.0.0 で削除、が実例です）。
- バージョン番号の定義箇所は `Directory.Build.props` の `<Version>` **1 箇所だけ**です。
  `.csproj` 側には書きません。
- リリースは `v<version>` 形式のタグ push で行います。
  `.github/workflows/release.yml` がタグ名と `<Version>` の一致を検証したうえで
  `dotnet pack` と NuGet への publish を実行します。

> **既知の逸脱:** v2.0.1 と v2.0.2 はこの方針に照らすと PATCH ではありません
> （詳細は各セクションを参照）。CHANGELOG 導入前のリリースで、遡って記載したものです。

## [3.0.0] - 未リリース

`TransactionManager` と `Optional<T>` 周りの修正、およびエンティティの等価性の修正。
**破壊的変更を含みます。**

### Breaking Changes

#### `EntityBase<TSelf, TIdentifier>`

- **等価性に実行時型の一致を追加しました。** これまでは `Identifier` だけで比較していたため、
  同じ基底を継承した別種のエンティティ（`Admin` と `Guest` がともに
  `User : EntityBase<User, UserId>` を継承しているような場合）が、識別子が一致するだけで
  等価になっていました。現在は等価になりません。
  `ValueObjectBase` について 0c8c6a7 で修正済みだった問題の、エンティティ側の対応です。
- `==` / `!=` の引数型を対称にしました
  （`(EntityBase<TSelf, TIdentifier>?, TSelf?)` → `(EntityBase<TSelf, TIdentifier>?, EntityBase<TSelf, TIdentifier>?)`）。
  既存の呼び出しはそのままコンパイルできます。基底型として宣言した変数どうしの比較も
  書けるようになりました。
- `GetHashCode` が実行時型を含むようになりました（`HashCode.Combine(GetType(), Identifier)`）。
  永続化されたハッシュ値に依存している場合は影響します。
- 識別子の比較に `EqualityComparer<TIdentifier>.Default` を使うようになりました。

#### `Optional<TValue>`

- `readonly struct` になりました。`in` 引数や `readonly` フィールド経由のアクセスで
  防御的コピーが発生しなくなります。
- `HasValue` / `Value` の `init` アクセサを削除。
  オブジェクト初期化子（`new Optional<int> { Value = 5 }`）では生成できなくなりました。
  `HasValue = false` なのに値だけ入っている矛盾状態を作れなくするためです。
  生成にはコンストラクター / `Optional<T>.Some(value)` / `Optional<T>.Empty` を使ってください。
  - **Mapster への影響**: `Optional<T>` が完全な不変型になったため、
    他の型から `Optional<T>` への**規約ベースのマッピングが動かなくなります**
    （`Cannot convert immutable type` で例外になります。値が黙って落ちることはありません）。
    `CSStack.TADA.MagicOnionHelper.Abstractions` の `MPOptional<T>` から変換する場合は、
    パッケージが提供する `ToOptional()` / `FromOptional()` を使うか、
    `config.NewConfig<MPOptional<T>, Optional<T>>().MapWith(src => src.ToOptional())` を登録してください。
    `Optional<T>` → `MPOptional<T>` 方向は従来どおり規約マッピングで動きます。
- `Optional(TValue? value, bool hasValue)` は `hasValue` が false のとき `value` を保持しなくなりました
  （None は常に `default` を保持します）。
- `OptionalExtensions.Exchange` を `[Obsolete]` にしました。`Map`（または `Select`）を使ってください。

#### `ITransactionService<TSession>`

- `BeginAsync` / `CommitAsync` / `RollbackAsync` に `CancellationToken` パラメーター（省略可）を追加。
  既存の実装はシグネチャの更新が必要です。
- 非ジェネリックな基底インターフェース `ITransactionService` を追加。
  `ITransactionService<TSession>` が既定実装を提供するため、実装側での対応は不要です。
- **セッションの所有権を明文化**: `BeginAsync` が返したセッションは `ITransactionManager` が所有し、
  commit / rollback / 例外のいずれの経路でも `ITransactionManager` が `Dispose` します。
  実装側で `CommitAsync` / `RollbackAsync` 内から `Dispose` してはいけません。

#### `ITransactionManager` / `TransactionManager`

- すべてのメソッドに `CancellationToken` パラメーター（省略可）を追加。
- `ExecuteTransactionAsync` の本体デリゲートが
  `Func<TransactionSessions, ValueTask>` から `Func<TransactionSessions, CancellationToken, ValueTask>` に変更。
- `TransactionManager` を `sealed` 化。
- `TransactionManager.Sessions` プロパティを削除。
  代わりに `GetSession<TSession>()` / `GetSession(Type)` / `TryGetSession<TSession>(out TSession)` を使ってください。
- `GetSession(Type)` の戻り値が `object` から `IDisposable` に変更。
- セッション未登録時に `KeyNotFoundException` ではなく `TransactionSessionNotFoundException` を送出。
- **トランザクションの入れ子が `NestedTransactionException` になりました。**
  実行中の `ExecuteTransactionAsync` の本体（および `beforeRollbackHandler`）から
  同じマネージャーの `ExecuteTransactionAsync` を呼ぶと例外になります。
  `ITransactionManager` は Scoped 登録なので、**コマンドサービスが別のコマンドサービスを呼ぶと
  これに当たります**。従来はコンパイルも実行も通っていましたが、静かに壊れていました
  （下記 Fixed を参照）。連続して 2 つのトランザクションを張るのは従来どおり正当です。
- `ITransactionManager` に以下を追加（実装クラスにしか無かったものを含む）:
  `BeginTransactionAsync(Type)` / `BeginTransactionsAsync(ImmutableList<Type>)` /
  `GetSession(Type)` / `TryGetSession<TSession>(out TSession)` / `GetTransactionService(Type)` /
  `ExecuteTransactionAsync` のジェネリックオーバーロード。
  `ITransactionManager` を自前で実装している場合は追加のメンバーが必要です。

#### `TransactionSessions`

- コンストラクターと `Sessions` プロパティの型が
  `IReadOnlyDictionary<Type, dynamic>` から `IReadOnlyDictionary<Type, IDisposable>` に変更。
- `GetSession<TSession>()` が未登録の型に対して `KeyNotFoundException` ではなく
  `TransactionSessionNotFoundException` を送出。

#### 例外クラス

- **内部例外のパラメーター名を `innserException` から `innerException` に修正しました**
  （`TADAException` / `DomainInvalidOperationException` / `ObjectAlreadyExistException` /
  `ObjectNotFoundException` / `ValueObjectInvalidException` / `ValueObjectLengthException` /
  `ValueObjectNullException` の 7 クラス）。
  綴り誤りのため `new ObjectNotFoundException(innerException: ex)` と書けず、
  `.NET` の慣習（`Exception(string, Exception)` の第 2 引数は `innerException`）にも反していました。
  **名前付き引数で `innserException:` と書いているコードはコンパイルエラーになります。**
  位置引数で渡している場合は影響ありません。

### Added

- `Optional<TValue>` が `IEquatable<Optional<TValue>>` を実装し、`==` / `!=` / `Equals` /
  `GetHashCode` を定義しました。既定の `ValueType.Equals` によるリフレクション比較ではなくなります。
  三状態は等価性でも区別されます:
  - `None == None` → true
  - `Some(null) == Some(null)` → true
  - `None == Some(null)` → **false**
- `Optional<TValue>.Match(onSome, onNone)`（`Action` 版と `Func` 版）。
- `Optional<TValue>.GetValueOrDefault(defaultValue)`（`GetValue` の慣用名）。
- `OptionalExtensions` に `Map` / `Select` / `Bind` / `SelectMany` / `Where` を追加。
  `Select` / `SelectMany` / `Where` が揃ったため LINQ クエリ構文が使えます。
- `TransactionSessionNotFoundException`（`TADAException` 派生）。
  セッション型名と対処法を含むメッセージ、および `SessionType` プロパティを持ちます。
- `NestedTransactionException`（`TADAException` 派生）。
  トランザクションを入れ子にしたときに送出されます。メッセージには
  「共通処理はドメインサービス／集約サービスに切り出し、同じ `ExecuteTransactionAsync` の
  本体から呼ぶ」という対処法まで含みます。
- `TransactionSessions.TryGetSession<TSession>(out TSession)`。
- `ITransactionManager.ExecuteTransactionAsync` のジェネリックオーバーロード（型引数 1〜3 個）。
  `ImmutableList.Create(typeof(MySession))` を書かずに済み、`IDisposable` でない型はコンパイルエラーになります。
- `IDisposable` を実装しないセッション型を `Type` 版 API に渡した場合の `ArgumentException`。
- `ObjectNotFoundException` / `ObjectAlreadyExistException` に、**対象の型と識別子を受け取る
  コンストラクター** `(Type objectType, object? identifier, string? message = null,
  Exception? innerException = null)` を追加しました。
  `ObjectType` / `Identifier` プロパティとして保持し、`message` を省略すると
  「どの型の、どの識別子が」を含むメッセージを自動生成します。
  これまではどちらの例外もコンテキストを一切持たず、ログから追跡できませんでした。
  既存のコンストラクターはそのまま残しているので非破壊です
  （`ObjectType` / `Identifier` は `null` になります）。

### Fixed

- **接続リーク**: commit / rollback / 例外のいずれの経路でもセッションが `Dispose` されていなかった。
  現在は begin と逆順ですべて `Dispose` されます。
- **rollback の中断**: 1 つのセッションのロールバックが失敗すると残りが放置されていた。
  現在はすべてのセッションを必ず処理し、失敗はまとめて報告されます
  （1 件ならその例外をそのまま、複数件なら `AggregateException`）。
- **commit の部分失敗**: 未 commit のセッションが放置されていた。
  現在は未 commit のセッションのロールバックを試みます（commit 済みの分は元に戻せません）。
- **キャンセル時の後始末**: ロールバックは常にキャンセルされていないトークンで実行されるため、
  `CancellationToken` のキャンセルでトランザクションが開いたまま残ることはありません。
- **入れ子による外側トランザクションの早期コミット**: 実行中の `ExecuteTransactionAsync` の
  本体からもう一度 `ExecuteTransactionAsync` を呼ぶと、内側の commit が
  **自分が開始していない外側のセッションまで commit / `Dispose`** していました。
  例外は一切出ず、外側の残りの処理は Dispose 済みセッションに対して動き、
  外側の commit は対象が空のため何もせず成功していたため、
  「ロールバックされるはずだった書き込み」が確定していました。
  現在は入り口で `NestedTransactionException` を投げます（上記 Breaking Changes を参照）。
- **`TransactionSessions` が可変辞書を直接参照していた**: `ExecuteTransactionAsync` の本体に
  マネージャー内部の辞書がそのまま渡っていたため、本体が `BeginTransactionAsync<TOther>()` を
  呼ぶと渡していないセッション型まで取得でき、逆に commit 後は中身が空になっていました。
  現在は呼び出し開始時点のコピーを渡します。
- **`BeginTransactionsAsync` の部分失敗**: 2 つ目以降の begin が失敗しても、
  開始済みのセッションがロールバック・`Dispose` されずに残っていました
  （`ExecuteTransactionAsync` 経由では `catch` が拾うため実害はありませんでしたが、
  公開メソッドとして単体で呼ぶとリークしていました）。
  現在は開始済みをロールバックしてから元の例外を再送出します。
- **AOT / trimming**: `src/` から `dynamic` を排除しました。
- **`Optional<T>` の null 許容解析**: `TryGetValue` の `out` パラメーターに `[MaybeNullWhen(false)]` を付与。
  false が返ったあとに `out` の値を使うとコンパイラが警告するようになります。
- **`OptionalExtensions` の XML doc 破損**: タグ内部に `///` が混入して `<see cref=...>` が
  解決できなくなっていた 5 箇所を修正しました（CS1570 警告がゼロになります）。
- **エンティティの誤った等価性**: 同じ基底を継承した別種のエンティティが、識別子の一致だけで
  等価になっていました（上記 Breaking Changes を参照）。

### Documentation

- **セッション型の扱いについての記述を全面的に改めました（API の変更はありません）。**
  これまで「ドメイン層のインターフェースに具体的なセッション型（`AppSession` 等）を書くのは
  `TSession` を明示的に伝播する設計の必然的な帰結であり、共有プロジェクトに置くのが推奨」と
  書いていましたが、**これは推奨しません**。ドメイン層がインフラ層の実トランザクション因子を
  名指しした時点で依存の向きが逆転し、TADA と DDD の利点がほぼ失われるためです。
  現在の規約は次のとおりで、`docs/`（architecture / getting-started / domain-model / use-case /
  best-practices / api-reference）・README（英日）・`CLAUDE.md` の地雷リスト・
  ドメイン層とユースケース層の XML doc・`samples/` の実装をこれに合わせました。
  - ドメイン層とユースケース層は**セッション型を型引数として受け取る**
    （`IUserRepository<TSession>` / `UserAggregateService<TSession>`）
  - 型引数の名前は、リポジトリと集約サービスは扱うリポジトリが 1 つなので `TSession`。
    **ドメインサービスとユースケースは複数の集約を跨ぎうるので `T[集約名]Session`**
    （`TUserSession` 等。集約ごとにデータストアが違えばセッション型も違うため、
    1 集約しか扱っていない段階からこう書く）
  - **具体型が決まるのはインフラ層の実装と、ユースケースとリポジトリを結びつける
    プレゼンテーション層（DI 登録・ユースケースの組み立て）だけ**
  - 境界の DTO（`ICommandServiceDTO` / `IQueryServiceDTO`）はセッションを持たないので
    型引数も取らない。セッションを載せる `IDomainServiceDTO` だけが型引数を取る
  - `docs/best-practices.md` に規約 11「ドメイン層・ユースケース層に具体的なセッション型を
    書かない」を追加し、README の地雷リストにも 9 項目目として追加しました
- **集約サービスとユースケースはインターフェースを立ててから実装する、という規約を追加しました
  （API の変更はありません）。** これまでは `AggregateServiceBase` を継承した具象クラスを
  そのままユースケースに注入し、ユースケースは `ICommandService<TReq, TRes>` で解決する形で
  書いていましたが、前者はユースケースのテストのために集約サービスの具象クラスと
  リポジトリ実装の組み立てを強制し、後者はプレゼンテーション層に
  `CreateUserCommandService<AppSession>` と書かせることになります。
  - **集約サービス**は `IAggregateService` を継承した口（`IUserAggregateService<TSession>`）を
    宣言し、その実装を `AggregateServiceBase` の派生クラスとして書く。
    **基底クラスは実装の詳細**であり、上の層に見せる契約ではない
  - **コマンドサービス**は**セッション型引数を持たない口**
    （`ICreateUserCommandService : ICommandService<...>`）を立てる。
    プレゼンテーション層はこれを解決するのでセッション型を書かずに済み、
    型引数が現れるのは DI 登録の 1 行だけになる
  - **ドメインサービスとクエリサービスにも口を立てる**（理由は下記の DTO の置き場所の規約）
  - `docs/best-practices.md` に規約 12 を追加し、README の地雷リストにも 10 項目目として
    追加しました。`docs/`（architecture / getting-started / domain-model / use-case /
    api-reference）・`CLAUDE.md`・`IAggregateService` / `AggregateServiceBase` /
    `ICommandService` / `IDomainServiceDTO` の XML doc・`samples/` を揃えています
- **集約サービスの口に `SaveAsync` のような汎用的な操作を置かない、という規約を追加しました
  （API の変更はありません）。** `IAggregateService` を継承した口は実質的に集約ルートであり、
  そこに並ぶメソッドが「この集約に何ができるか」の一覧になります。何でも受け取る `SaveAsync` が
  `RegisterAsync` と並んでいると、呼ぶ側はたいてい `SaveAsync` を選び、
  `RegisterAsync` に置いた「既に居たら失敗」が素通りされ、
  リポジトリの upsert によって黙って上書きされます。
  - 「取得する → 変更する → 保存する」は `RenameAsync` のような**ドメインの語彙で名付けた
    1 つの操作**に閉じる。エンティティを集約の外へ出さないので、保存忘れも起こらない
  - 不在を `ObjectNotFoundException` にするヘルパー（`GetRequiredAsync`）は `private` に留める。
    口に載せるとエンティティが上の層へ出てしまい、書き換えても保存する手段が無い状態を作れる
  - `docs/best-practices.md` に規約 13 を追加し、`docs/`（architecture / getting-started /
    domain-model / api-reference）・README（英日）・`IAggregateService` /
    `AggregateServiceBase` / `IRepository` の XML doc・`samples/` を揃えています
- **リクエスト / レスポンス DTO を、それを使うサービスの口の中に `Req` / `Res` として
  ネストする規約を追加しました（API の変更はありません）。**
  `ICommandService` / `IQueryService` / `IDomainService` を継承した時点で
  「メソッドは 1 つ、リクエスト 1 型、レスポンス 1 型」が確定するので、DTO は口と 1 対 1 に
  対応します。名前空間に平らに置くと、口に対応する DTO を名前で探すことになり、
  別のユースケースの DTO を渡しても型が合えばコンパイルが通ります。
  - `ICreateUserCommandService.Req` / `.Res` のように口から必ず辿れる位置に置く
  - **ドメインサービスとクエリサービスに口を立てる理由もこれ。** 前者は
    `IDomainService<TReq>` が DTO の型で一意に定まるため注入だけなら口は不要、
    後者はセッション型引数を持たないが、いずれも DTO の置き場所として口が要る
  - 複数の口で共有する読み取りモデル（一覧の 1 行など）は 1 対 1 ではないのでネストしない
  - `docs/best-practices.md` に規約 14 を追加し、`docs/`（use-case / architecture /
    getting-started / domain-model / api-reference）・README（英日）・
    `ICommandService` / `IDomainService` / `IQueryService` と 3 つの DTO マーカーの
    XML doc・`samples/` を揃えています
- **`docs/domain-model.md` を追加**。ドメイン層の型が何を強制し、何を規約に留めているかを整理:
  1 集約 = エンティティ 1・リポジトリ 1・集約サービス 1 という `IAggregateService` の
  5 型引数の意図 / エンティティの等価性と `Create`・`Reconstruct` の推奨 /
  値オブジェクトは `record` で実装し検証は `Create` に書くこと /
  `TOperateInfo` の意味と読み取りで受け取らない理由 / `SaveAsync` が upsert であること /
  `IRepository` に検索系メソッドを置かない理由 / 各例外をどの層が投げるか。
- **`docs/use-case.md` を追加**。`ICommandService` / `IQueryService` / `IDomainService` の
  使い分け、コマンドサービスが `ITransactionManager` でトランザクションを包む規約とサンプル、
  DI 登録、`IQueryService<TRes>` だけ型引数の意味が逆転している点、DTO に載せるもの。
- ドメイン層・ユースケース層の XML doc に上記の設計意図を反映しました。
  これまで宣言だけでは読み取れなかった規約（`TOperateInfo` とは何か、`SaveAsync` の
  セマンティクス、検証の置き場所、例外を投げる層）が型の側から辿れるようになります。
- README に `docs/` への導線と、主要コンポーネントの補足を追加しました。
  トランザクション管理のサンプルにあった `SaveAsync` の引数順の誤りも修正しています。
- **`Some(null)` の罠**を XML doc に明記:
  - 暗黙変換 / `Optional(TValue)` / `Some(TValue)` は null を渡すと `None` ではなく
    `Some(null)` を生成すること
  - `IRepository.FindByIdentifierAsync` は「見つからなかった」を `Optional<T>.Empty` で返すこと。
    `return null;` と書くと `HasValue = true` かつ値が null の状態になり、
    呼び出し側の `TryGetValue` が true を返したうえで `NullReferenceException` になります
  - 型引数のnull許容性を正しく宣言すること（null が正当な値なら `Optional<string?>`、
    そうでなければ `Optional<User>`）。コンパイラの null 許容解析はこの宣言に従います
- **`docs/optional.md` を追加**。三状態が必要な理由（PATCH の部分更新）、`return null` の罠、
  型引数の null 許容性の宣言、生成・取り出し・変換の API、等価性、Mapster の `MapWith` を解説。
  README にも `Optional<T>` の節（英日）を追加しました。
- **`CLAUDE.md` を追加**。コーディング規約と、利用時に踏みやすい 4 つの落とし穴を集約。
- `ITransactionManager` / `TransactionManager` の XML doc に以下を明記:
  - 複数セッションの commit は**アトミックではない**こと（2 相コミットではない）
  - `TransactionManager` はスレッドセーフではなく、**Scoped で登録すること**
  - セッションの所有権は `ITransactionManager` にあること
- **空だった XML doc タグを埋めました**（`ITransactionManager` / `ITransactionService` /
  `ICommandService` の `<returns>` 計 11 箇所）。特に
  `ExecuteTransactionAsync` の `sessionTypes` / `transactionFunction` / `beforeRollbackHandler` は、
  挙動が宣言から読み取れなかったため書き下しています:
  - `beforeRollbackHandler` が**例外を投げてもロールバックは中断されず**、
    元の失敗と併せて報告されること
  - `transactionFunction` がセッションを commit / rollback / dispose してはいけないこと
  - `sessionTypes` に挙げていないセッションを要求すると
    `TransactionSessionNotFoundException` になること
- **`beforeRollbackHandler` の呼び出し位置の記述を実態に合わせました**（XML doc と
  `docs/api-reference.md`）。「常にロールバック前に呼ばれる」と書いていましたが、
  **commit が失敗した経路ではロールバック後**に呼ばれます。
  `CommitTransactionsAsync` が内部でロールバックと `Dispose` を済ませてから throw するためで、
  この経路ではハンドラから見えるセッションは既に `Dispose` 済みです。
  挙動は変えていません（呼び出し順序を固定するテストを追加しました）。
- **入れ子の禁止**を `CLAUDE.md` の地雷・README の地雷・`docs/best-practices.md` /
  `docs/use-case.md` / `docs/api-reference.md` / `docs/getting-started.md` /
  `docs/migration.md` に追記しました。
- `ValueObjectLengthException` の XML doc を補強しました。
  引数が **3 つとも `int`** で順序を誤ってもコンパイルが通るため、
  引数の意味（境界値が先、弾かれた長さが最後）と名前付き引数の推奨を明記し、
  `docs/domain-model.md` のサンプルも名前付き引数に改めています。
- `docs/domain-model.md` に `ObjectNotFoundException` / `ObjectAlreadyExistException` へ
  対象型と識別子を渡す節を追加しました。
- **`README.md` を全面改稿しました。** これまで README には**コード例が 1 つもなく**、
  NuGet パッケージ名もインストール手順も書かれていませんでした。現在は英日それぞれに
  インストール手順 / **貼り付ければ動く 8 ステップの最小例**（セッション定義 → 値オブジェクト →
  エンティティ → リポジトリ → トランザクションサービス → コマンドサービス → DI 登録 → 実行）/
  レイヤー構成図 / **公開型 34 個すべての 1 行説明** / 必ず踏む地雷 10 項目 / `docs/` への導線が
  揃っています。
- **`docs/architecture.md` を追加**。TADA の設計思想:
  トランザクションの範囲の表し方が 3 通りあり、暗黙の文脈（`TransactionScope`）と
  リポジトリがセッションを握る方式のそれぞれが何を壊すか / **なぜ `TSession` を
  全レイヤーに引き回すのか**とその代償 / レイヤー構成と `src/` のフォルダとの対応 /
  セッション伝播モデル / DDD・クリーンアーキテクチャとの差分 4 点 /
  ドメイン層に具体的なセッション型を書かない理由と、型引数の名前・確定させる層の規約。
- **`docs/getting-started.md` を追加**。インストールから動くまでを 7 ステップに分解。
  **DI 登録（`ITransactionService<TSession>` の登録と `TransactionManager` の Scoped 必須）**を
  独立した章に置き、落とすと必ず落ちる 2 つを明示しています。症状から原因を引く表つき。
- **`docs/best-practices.md` を追加**。放置すると必ず踏む規約 12 件を危険度順に、
  「間違い → 正しい形 → なぜ」の形で集約しました。将来 skill 化する際の参照先を想定しています。
- **`docs/api-reference.md` を追加**。公開型 34 個すべてと、型引数
  （`TEntity` / `TEntityIdentifier` / `TOperateInfo` / `TSession` / `TRepository` /
  `TReq` / `TRes` / `TSelf`）の意味を一覧化しました。
- **`docs/migration.md` を追加**。v2.x → v3.0.0 と v1.x → v2.0.x の移行手順。
  コンパイルエラーになるものと、**コンパイルが通るのに挙動が変わるもの**
  （エンティティの等価性、`Optional<T>` の等価性、Mapster、ロールバックの挙動）を
  分けて記載しています。
- **`samples/CSStack.TADA.Sample/` を追加**。エンドツーエンドで動く最小のサンプルで、
  外部ミドルウェアを必要としません。値オブジェクト / エンティティ / リポジトリ /
  集約サービス / ドメインサービス / コマンドサービス / クエリサービス /
  トランザクションサービス / DI 登録がひととおり揃っており、
  ロールバックとセッションの所有権を実際に出力して見せます。
  `CSStack.TADA.sln` に含め、**CI がビルドと実行まで行う**ので、
  API を変えたままサンプルが古くなると CI が落ちます。

### Repository / ビルド

パッケージのライセンス表記以外は利用者から見える変更ではありませんが、リポジトリ側の整備です。

- **`PackageLicenseExpression` の値が `" MIT"`（先頭に半角スペース）になっていたのを修正しました。**
- `.editorconfig` を追加しました。`src/` はタブ・`tests/` と `samples/` はスペース 4、`.cs` は UTF-8 BOM 付き、
  namespace はフォルダ構成を反映せずフラット（`dotnet_style_namespace_match_folder = false`）、
  ブロックスコープの namespace、という既存の規約を機械的に強制します。
- `Directory.Build.props` を追加し、`Version` / `Authors` / ライセンス / リポジトリ URL と
  `LangVersion` / `Nullable` / `ImplicitUsings` を集約しました。
  `TreatWarningsAsErrors` を有効にしたため、XML doc の破損（CS1570 / CS1574）や
  記述漏れ（CS1591）でビルドが失敗します（コード側で直せない NU1902 / NU1903 は除外）。
- GitHub Actions を追加しました。push / PR で net8.0・net10.0 の両方をビルドしてテストし
  （`.github/workflows/ci.yml`）、`v*` タグの push で pack して NuGet に publish します
  （`.github/workflows/release.yml`）。CI は `samples/` のビルドと実行も行います。
- `src/CSStack.TADA/UseCase/UseCase/` の二重フォルダを `src/CSStack.TADA/UseCase/` に解消しました。
  namespace はすべて `CSStack.TADA` でフラットなため API に影響はありません。

## [2.0.2] - 2026-06-21

> **遡及記載**: CHANGELOG 導入前のリリースをコミット履歴から再構成したものです。
> PATCH として出していますが、内容は下記のとおり MAJOR 相当です。

### Added

- `ITransactionManager` に、それまで実装クラス `TransactionManager` にしか無かったメンバーを追加:
  `BeginTransactionAsync<TSession>()` / `CommitTransactionsAsync()` /
  `RollbackTransactionsAsync()` / `GetSession<TSession>()` /
  `GetTransactionService<TSession>()`。
  インターフェース越しに個別のトランザクション操作が呼べるようになりました。

### Breaking Changes

- 上記はいずれも既定実装のないインターフェースメンバーの追加のため、
  **`ITransactionManager` を自前で実装していた場合はコンパイルエラーになります。**
  `TransactionManager` をそのまま使っていた場合は影響ありません。

## [2.0.1] - 2026-06-21

> **遡及記載**: 同上。PATCH として出していますが、内容は MAJOR 相当です。

### Removed

- `IValueObject.Validate()` を削除しました。`IValueObject` はメンバーを持たない
  マーカーインターフェースになっています。
- `IEntity<TIdentifier>` から `Validate()` と `IsInvalidValue` を削除し、
  `EntityBase<TSelf, TIdentifier>` の対応する実装も削除しました。
- `ValueObjectExtensions`（`IValueObject` に `IsInvalidValue` を生やす拡張）を削除しました。
  v1.1.0 で `ValueObjectBase.IsInvalidValue` の移行先として用意したものですが、
  結局この版で役目を終えています。

### 移行方法

検証は**生成時に済ませる**方針に変わりました。値オブジェクトは `record` で実装し、
検証は `Create` の中に書きます。永続化からの復元である `Reconstruct` では検証しません。
不正な値を保持したまま存在して、後から `Validate()` で確かめるオブジェクトは作りません。
`Validate` メンバーはライブラリのどこにも存在しません。
→ [docs/domain-model.md](docs/domain-model.md)

## [2.0.0] - 2026-06-21

> **遡及記載**: CHANGELOG 導入前のリリースをコミット履歴から再構成したものです。

v1.1.0 で `[Obsolete]` にした型を一括削除し、外部依存をゼロにしたリリースです。

### Removed

削除した型はすべて v1.1.0 で `[Obsolete]` としていたものです。

| 削除した型 | 移行先 |
|---|---|
| `ValueObjectBase` | `IValueObject` を直接実装する（`record` で実装し、検証は `Create` の中に書く） |
| `ValidateHelper` | 例外を集約せず、`Create` やコンストラクターでガード節を書く |
| `KeyedValidateHelper<TKey>` | 同上。UI 向けのエラー集約はアプリケーション層／プレゼンテーション層で行う |
| `MultiReasonException` | 同上 |
| `KeyedMultiReasonException<TKey>` | 同上 |
| `IDomainServiceWithRes<TReq, TRes>` | `IDomainService<TReq, TRes>` |
| `ICommandServiceWithRes<TReq, TRes>` | `ICommandService<TReq, TRes>` |
| `IQueryServiceWithoutReq<TRes>` | `IQueryService<TRes>` |

`WithRes` / `WithoutReq` の 3 つは、同名のジェネリック型にオーバーロードが用意されたことで
不要になったものです。型名を差し替えるだけで移行できます。

`ValueObjectBase` の `IsInvalidValue` は、この版では拡張メソッドとして
`Extensions/ValueObjectExtensions.cs` に移されています。
**ただしこの拡張自体も v2.0.1 で削除されました**（上記参照）。
現在の方針は「生成時に検証し、不正なインスタンスを作らせない」です。

### Changed

- **外部依存をゼロにしました。** `Microsoft.Extensions.DependencyInjection.Abstractions` 8.0.0 への
  `PackageReference` を削除しています。`TransactionManager` が必要とするのは
  `System.IServiceProvider` だけなので公開 API は変わりませんが、
  このパッケージが**推移的に流れてこなくなります**。
  暗黙に依存していたプロジェクトは自分で `PackageReference` を追加してください。
- `ISingleValueObject<TValue, TSelf>` のメンバー順を整理しました（API の変更はありません）。

### Added

- `ISingleValueObject<TValue>`。`Value` だけを持つ型引数 1 つ版で、
  `static abstract` な `Create` / `Reconstruct` を要求しません。
  「単一の値を包んでいる」ことだけを表明したい場合に使います。
  従来の `ISingleValueObject<TValue, TSelf>` はそのまま残っています。
- `OptionalExtensions`（`CreateSingleValueObject` / `ReconstructSingleValueObject` /
  `Exchange` / `ExchangeValueObjectToPrimitive`）。
  なお `Exchange` は v3.0.0 で `[Obsolete]` になり、`Map` / `Select` に置き換わっています。

## [1.1.0] 以前

CHANGELOG 導入前のため詳細な記録はありません。コミット履歴を参照してください。

v1.1.0（2026-06-04）では .NET 10 に対応し、v2.0.0 で削除した型を `[Obsolete]` としています。
`ValueObjectBase` の等価性の欠陥（継承先が異なる値オブジェクトどうしが等価になる）は
この版で修正されました。同じ問題のエンティティ側の対応は v3.0.0 です。

[3.0.0]: https://github.com/Uta-member/CSStack.TADA/compare/v2.0.2...HEAD
[2.0.2]: https://github.com/Uta-member/CSStack.TADA/compare/v2.0.1...v2.0.2
[2.0.1]: https://github.com/Uta-member/CSStack.TADA/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/Uta-member/CSStack.TADA/compare/v1.1.0...v2.0.0
[1.1.0]: https://github.com/Uta-member/CSStack.TADA/releases/tag/v1.1.0
