# CLAUDE.md

Transaction-Aware Domain Architecture (TADA) を実装するための C# ライブラリ。
NuGet パッケージ名は `CSStack.TADA`、`net8.0;net10.0` のマルチターゲット。

## コーディング規約

ルートの `.editorconfig` がこれらを機械的に強制する。規約を変えるときは
`.editorconfig` と CLAUDE.md の両方を更新すること。

- **インデント**: `src/` はタブ。`tests/` と `samples/` は半角スペース 4 つ（既存ファイルに合わせる）
- **namespace はフラットに `CSStack.TADA`**。`Domain/` `UseCase/` などのフォルダ構成を
  namespace に反映しない（`CSStack.TADA.Domain` と書かない）。テストは `CSStack.TADA.Tests`、
  サンプルは `CSStack.TADA.Sample`。
  利用者が `using CSStack.TADA;` だけで済むようにするための意図的な設計で、
  `.editorconfig` の `dotnet_style_namespace_match_folder = false` に対応する
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
   → [docs/best-practices.md](docs/best-practices.md)
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
   なぜセッションを引き回すのかは → [docs/architecture.md](docs/architecture.md)
8. **`ObjectNotFoundException` を投げるのはリポジトリではない。** 不在は
   `Optional<T>.Empty` で返り、それを異常とみなすかは集約サービス / ユースケースが決める
9. **トランザクションは入れ子にできない。** 実行中の `ExecuteTransactionAsync` の本体から
   同じマネージャーの `ExecuteTransactionAsync` を呼ぶと `NestedTransactionException`。
   `ITransactionManager` は Scoped なので、**コマンドサービスが別のコマンドサービスを呼ぶと
   これに当たる**。共通処理はドメインサービス / 集約サービスに切り出し、同じトランザクションの
   本体からセッションを渡して呼ぶ。連続して 2 つのトランザクションを張るのは正当
   → [docs/best-practices.md](docs/best-practices.md)
9. **ドメイン層・ユースケース層に具体的なセッション型を書かない。**
   `IUserRepository : IRepository<User, UserId, OperateInfo, AppSession>` はコンパイルは通るが、
   ドメイン層がインフラ層の実トランザクション因子を名指しした時点で依存の向きが逆転し、
   TADA と DDD の利点がほぼ消える。**セッション型は型引数として受け取る**
   （`IUserRepository<TSession>` / `UserAggregateService<TSession>`）。
   名前は、リポジトリと集約サービスは扱うリポジトリが 1 つなので `TSession`、
   **ドメインサービスとユースケースは複数集約を跨ぎうるので `T[集約名]Session`**
   （`TUserSession` など。1 集約しか扱っていなくてもこう書く。集約ごとにストアが違えば
   セッション型も違うため）。**具体型が決まるのはインフラ層の実装と、
   ユースケースとリポジトリを結びつけるプレゼンテーション層の DI 登録だけ**
   → [docs/architecture.md](docs/architecture.md)
10. **4 種のサービスはすべてインターフェースを立ててから実装する。**
    集約サービスは `IAggregateService` を継承した口
    （`IUserAggregateService<TSession>`）を宣言し、その実装を `AggregateServiceBase` の
    派生クラスとして書く。**基底クラスは実装の詳細**で、上の層に見せる契約ではない
    （具象を注入すると、ユースケースのテストが集約サービスとリポジトリ実装の組み立てになる）。
    ユースケースは**セッション型引数を持たない口**（`ICreateUserCommandService :
    ICommandService<...>`）を立てる。こうするとプレゼンテーション層は
    `<AppSession>` を書かずに解決・実行でき、型引数が現れるのは DI 登録の 1 行だけになる。
    ドメインサービスとクエリサービスにも口を立てる（理由は 12 の DTO の置き場所）
    → [docs/best-practices.md](docs/best-practices.md)
11. **集約サービスの口に `SaveAsync` のような汎用的な操作を置かない。**
    `IAggregateService` を継承した口は実質的に集約ルートで、並ぶメソッドが
    「この集約に何ができるか」の一覧になる。何でも受け取る `SaveAsync` が `RegisterAsync` と
    並んでいれば呼ぶ側はそちらを選び、「既に居たら失敗」が素通りして upsert で黙って上書きされる。
    **「取得する → 変更する → 保存する」は `RenameAsync` のようなドメインの語彙の
    1 つの操作に閉じる。** エンティティを上の層へ返さない（返すと、書き換えても保存する手段が
    口に無い状態が作れる）。`GetRequiredAsync` のようなヘルパーは実装側で `private` に留める
    → [docs/best-practices.md](docs/best-practices.md)
12. **リクエスト / レスポンスはそれを使う口の中に `Req` / `Res` としてネストする。**
    `ICommandService` / `IQueryService` / `IDomainService` を継承した時点で
    「メソッド 1 つ・リクエスト 1 型・レスポンス 1 型」が確定するので、DTO は口と 1 対 1 に対応する。
    `ICreateUserCommandService.Req` と口から辿れる位置に置く。名前空間に平らに置くと、
    別のユースケースの DTO を渡しても型が合えばコンパイルが通る。
    ドメインサービスの `Req` は口の型引数（`TUserSession`）をそのまま使ってセッションを載せる。
    複数の口で共有する読み取りモデルは 1 対 1 ではないのでネストしない
    → [docs/use-case.md](docs/use-case.md)
13. **トランザクションは入れ子にできない。** 実行中の `ExecuteTransactionAsync` の本体から
   同じマネージャーの `ExecuteTransactionAsync` を呼ぶと `NestedTransactionException`。
   `ITransactionManager` は Scoped なので、**コマンドサービスが別のコマンドサービスを呼ぶと
   これに当たる**。共通処理はドメインサービス / 集約サービスに切り出し、同じトランザクションの
   本体からセッションを渡して呼ぶ。連続して 2 つのトランザクションを張るのは正当
   → [docs/best-practices.md](docs/best-practices.md)

## ビルド・テスト

```
dotnet build      # net8.0 と net10.0 の両方
dotnet test
dotnet format     # .editorconfig に沿って自動整形。CI は --verify-no-changes で検査する
dotnet pack
dotnet run --project samples/CSStack.TADA.Sample   # サンプルの動作確認
```

警告ゼロを維持すること。`Directory.Build.props` で `TreatWarningsAsErrors` を有効にしているため、
XML doc の破損（CS1570 / CS1574）や記述漏れ（CS1591）は**ビルドエラーになる**。

**`dotnet format --verify-no-changes` も CI のゲート**になっている。インデント・改行コード
（作業ツリーは CRLF。`.gitattributes` の `eol=crlf` により OS を問わずそうなる）・命名規則の
違反はここで落ちるので、コミット前に `dotnet format` を通すこと。

push / PR では `.github/workflows/ci.yml` が同じことを CI で実行する。
リリースは `v<version>` タグの push で `.github/workflows/release.yml` が pack と publish を行い、
このときタグ名と `Directory.Build.props` の `<Version>` の一致が検証される。

## リポジトリ構成

```
.editorconfig             コーディング規約（インデント・BOM・namespace 方針・XML doc 診断）
Directory.Build.props     Version / パッケージメタデータ / LangVersion / 警告設定
.github/workflows/        CI（build + test）と Release（pack + NuGet publish）
src/CSStack.TADA/         ライブラリ本体
  Domain/                 Entity / ValueObject / Repository / DomainService / AggregateService
  UseCase/                TransactionService / CommandService / QueryService
  Exceptions/             TADAException とその派生
  Extensions/             OptionalExtensions
  Utilities/              Optional
tests/                    テスト（xUnit）
samples/CSStack.TADA.Sample/  エンドツーエンドの動くサンプル（CI でビルド＋実行）
docs/                     ドキュメント
  architecture.md         設計思想。なぜ TSession を引き回すのか
  getting-started.md      ゼロから動かすまでの 7 ステップ（DI 登録を含む）
  best-practices.md       規約の一覧。間違い → 正しい形 → なぜ
  api-reference.md        公開型 35 個と型引数の意味
  api-reference.md        公開型 35 個と型引数の意味
  domain-model.md         エンティティ / 値オブジェクト / リポジトリ / 集約
  use-case.md             3 種のサービスとトランザクションの境界
  optional.md             Optional<T> の三状態
  migration.md            v1 → v2 → v3 の移行手順
.todo/                    ローカル作業メモ（コミット対象外）
```

サンプルは `CSStack.TADA.sln` に含まれ、CI がビルドと**実行**まで行う。
`docs/` から参照する動くコードはここに置くと腐らない。

`.csproj` にバージョンやライセンスを書かない。パッケージ共通のメタデータは
`Directory.Build.props` が唯一の定義箇所。

## 作業するときの注意

- **`.todo/` はコミット対象外**（`.git/info/exclude` で除外）。改善タスクの一覧と
  詳細はここにある。作業を始める前に `.todo/README.md` を見る
- **仕様を変えたら XML doc と `docs/` の両方を更新する。** 片方だけ更新して乖離したことが
  過去の問題を生んだ構造
- **破壊的変更は `CHANGELOG.md` に記載する。** バージョンは Semantic Versioning に従う
- ドキュメントを書き上げたら早めにコミットする（未コミットのまま成果物を失った事故がある）
