# Changelog

このファイルは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に沿って記述し、
バージョンは [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [3.0.0]

`TransactionManager` と `Optional<T>` 周りの修正。**破壊的変更を含みます。**

### Breaking Changes

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
- `TransactionSessions.TryGetSession<TSession>(out TSession)`。
- `ITransactionManager.ExecuteTransactionAsync` のジェネリックオーバーロード（型引数 1〜3 個）。
  `ImmutableList.Create(typeof(MySession))` を書かずに済み、`IDisposable` でない型はコンパイルエラーになります。
- `IDisposable` を実装しないセッション型を `Type` 版 API に渡した場合の `ArgumentException`。

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
- **AOT / trimming**: `src/` から `dynamic` を排除しました。
- **`Optional<T>` の null 許容解析**: `TryGetValue` の `out` パラメーターに `[MaybeNullWhen(false)]` を付与。
  false が返ったあとに `out` の値を使うとコンパイラが警告するようになります。
- **`OptionalExtensions` の XML doc 破損**: タグ内部に `///` が混入して `<see cref=...>` が
  解決できなくなっていた 5 箇所を修正しました（CS1570 警告がゼロになります）。

### Documentation

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

## [2.0.2] 以前

CHANGELOG 導入前のため記録なし。コミット履歴を参照してください。
