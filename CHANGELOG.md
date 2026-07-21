# Changelog

このファイルは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に沿って記述し、
バージョンは [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [3.0.0]

`TransactionManager` 周りの修正。**破壊的変更を含みます。**

### Breaking Changes

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

### Documentation

- `ITransactionManager` / `TransactionManager` の XML doc に以下を明記:
  - 複数セッションの commit は**アトミックではない**こと（2 相コミットではない）
  - `TransactionManager` はスレッドセーフではなく、**Scoped で登録すること**
  - セッションの所有権は `ITransactionManager` にあること

## [2.0.2] 以前

CHANGELOG 導入前のため記録なし。コミット履歴を参照してください。
