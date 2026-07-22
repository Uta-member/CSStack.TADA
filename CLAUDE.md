# CLAUDE.md

Transaction-Aware Domain Architecture (TADA) を実装するための C# ライブラリ。
NuGet パッケージ名は `CSStack.TADA`、`net8.0;net10.0` のマルチターゲット。

## コーディング規約

- **インデント**: `src/` はタブ。`tests/` は半角スペース 4 つ（既存ファイルに合わせる）
- **namespace はフラットに `CSStack.TADA`**。`Domain/` `UseCase/` などのフォルダ構成を
  namespace に反映しない（`CSStack.TADA.Domain` と書かない）。テストは `CSStack.TADA.Tests`
- **ブロックスコープの namespace** を使う（`namespace X { ... }`）。file-scoped ではない
- **`.cs` は UTF-8 BOM 付き**。`.md` は BOM なし
- **すべての公開メンバーに XML doc が必須**（`GenerateDocumentationFile` が有効。警告ゼロを維持する）
- 型のメンバーはおおむねアルファベット順に並んでいる。既存の並びを崩さない
- 言語: XML doc は英語 / README は日英併記 / `docs/`・CHANGELOG・テストメソッド名・
  コミットメッセージは日本語

## このライブラリの地雷（利用者コードでも必ず踏む）

1. **`Optional<T>` で `return null;` と書かない。** 暗黙変換によって `None` ではなく
   `Some(null)`（`HasValue = true`）になる。不在は `Optional<T>.Empty` を返す。
   型引数の null 許容性も正しく宣言する（null が正当なら `Optional<string?>`、
   そうでなければ `Optional<User>`）→ [docs/optional.md](docs/optional.md)
2. **セッションの所有権は `ITransactionManager` にある。** commit / rollback / 例外の
   いずれの経路でも `ITransactionManager` が `Dispose` する。
   `ITransactionService<TSession>` の実装側で `Dispose` してはいけない
3. **`TransactionManager` は Scoped で登録する。** スレッドセーフではなく、実行中の
   セッションを保持するため、Singleton にすると全リクエストで混線する
4. **複数セッションの commit はアトミックではない。** 2 相コミットではないので、
   2 つ目が失敗しても 1 つ目は確定したまま残る
5. **`IRepository` に検索系メソッドを足さない。** あるのは `FindByIdentifierAsync` と
   `SaveAsync`（upsert）だけ。一覧取得・条件検索は `IQueryService` の仕事
   → [docs/domain-model.md](docs/domain-model.md)
6. **値オブジェクトは `record` で実装する。** 検証は `Create` の中に書き、
   `Reconstruct`（永続化からの復元）では検証しない。`Validate` メンバーは存在しない
7. **トランザクションの境界は `ICommandService`。** `ITransactionManager` を注入して
   `ExecuteTransactionAsync` で包み、セッションを下の層へ渡す。それより下の層は
   トランザクションを開始しない → [docs/use-case.md](docs/use-case.md)
8. **`ObjectNotFoundException` を投げるのはリポジトリではない。** 不在は
   `Optional<T>.Empty` で返り、それを異常とみなすかは集約サービス / ユースケースが決める

## ビルド・テスト

```
dotnet build      # net8.0 と net10.0 の両方
dotnet test
dotnet pack
```

警告ゼロを維持すること。特に XML doc の破損（CS1570）はレビューで見落とされやすい。

## リポジトリ構成

```
src/CSStack.TADA/     ライブラリ本体
  Domain/             Entity / ValueObject / Repository / DomainService / AggregateService
  UseCase/            TransactionService / CommandService / QueryService
  Exceptions/         TADAException とその派生
  Extensions/         OptionalExtensions
  Utilities/          Optional
tests/                テスト（xUnit）
docs/                 ドキュメント
.todo/                ローカル作業メモ（コミット対象外）
```

## 作業するときの注意

- **`.todo/` はコミット対象外**（`.git/info/exclude` で除外）。改善タスクの一覧と
  詳細はここにある。作業を始める前に `.todo/README.md` を見る
- **仕様を変えたら XML doc と `docs/` の両方を更新する。** 片方だけ更新して乖離したことが
  過去の問題を生んだ構造
- **破壊的変更は `CHANGELOG.md` に記載する。** バージョンは Semantic Versioning に従う
- ドキュメントを書き上げたら早めにコミットする（未コミットのまま成果物を失った事故がある）
