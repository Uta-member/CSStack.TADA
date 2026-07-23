# samples

CSStack.TADA をエンドツーエンドで使う最小のサンプル。

```
dotnet run --project samples/CSStack.TADA.Sample
```

外部ミドルウェアは要らない。「データベース」はプロセス内の `Dictionary` で、
セッションはその上の単純な unit of work になっている。

## なぜプロジェクトとして置いてあるか

README や `docs/` に貼ったコードは、API が変わっても誰も気づかないまま古くなる。
このサンプルは `CSStack.TADA.sln` に含まれていて CI がビルドと**実行**まで行うので、
壊れたら CI が落ちる。`docs/` から参照する動く例はここに置く。

## 何を見せているか

| ファイル | 見るべき点 |
|---|---|
| [Program.cs](CSStack.TADA.Sample/Program.cs) | **DI 登録**（`TransactionManager` は Scoped、`ITransactionService<TSession>` の登録）とシナリオ 7 本 |
| [Domain/UserId.cs](CSStack.TADA.Sample/Domain/UserId.cs) | 値オブジェクト。検証は `Create`、`Reconstruct` は検証しない |
| [Domain/UserName.cs](CSStack.TADA.Sample/Domain/UserName.cs) | `ILengthDefinedSingleValueObject` で長さの制約を公開する |
| [Domain/User.cs](CSStack.TADA.Sample/Domain/User.cs) | `EntityBase` による識別子の等価性。1 エンティティで完結するルールの置き場所 |
| [Domain/IUserRepository.cs](CSStack.TADA.Sample/Domain/IUserRepository.cs) | 検索系メソッドを足さない |
| [Domain/UserAggregateService.cs](CSStack.TADA.Sample/Domain/UserAggregateService.cs) | `Optional.Empty` を `ObjectNotFoundException` に変えるのはこの層 |
| [Domain/UserNameUniquenessService.cs](CSStack.TADA.Sample/Domain/UserNameUniquenessService.cs) | 集約をまたぐルール。DTO にセッションを載せる |
| [Infrastructure/AppTransactionService.cs](CSStack.TADA.Sample/Infrastructure/AppTransactionService.cs) | **セッションを Dispose しない**（所有権はマネージャー側） |
| [Infrastructure/InMemoryUserRepository.cs](CSStack.TADA.Sample/Infrastructure/InMemoryUserRepository.cs) | 不在は `Optional<User>.Empty`。`return null;` と書かない |
| [UseCase/CreateUserCommandService.cs](CSStack.TADA.Sample/UseCase/CreateUserCommandService.cs) | **トランザクションの境界**。`ExecuteTransactionAsync` で包む |
| [UseCase/UserQueryServices.cs](CSStack.TADA.Sample/UseCase/UserQueryServices.cs) | クエリはリポジトリを通さずストアを直接読む |

## 実行結果

シナリオ 3 では意図的に失敗させてロールバックを見せている。
例外が飛んだ後も登録件数が変わらないのが確認できる。
シナリオ 7 では、`ITransactionService` 側で Dispose していないにもかかわらず
セッションがコミット後に `IsDisposed = true` になることを表示する。

## 関連ドキュメント

- [../docs/getting-started.md](../docs/getting-started.md) — このサンプルをゼロから組み立てる手順
- [../docs/architecture.md](../docs/architecture.md) — なぜこの形になるのか
- [../docs/best-practices.md](../docs/best-practices.md) — ここで守っている規約の一覧
