# CSStack.TADA 使い方ガイド（日本語）

本ドキュメントは CSStack.TADA に含まれる各クラス・インターフェースの使い方を体系的かつ詳細に説明します。対象は .NET 8 / C# 12 です。

目次
- コア概念とレイヤリング
- エンティティ（値オブジェクトの使用、非公開コンストラクタ、Create/Reconstruct、イミュータブル更新）
- 値オブジェクト（ValidateHelper/KeyedValidateHelper）
- リポジトリ
- 集約サービス（AggregateService = 集約ルート、TSession のみを外部から受け取る）
- ドメインサービス（複数集約をまたぐ・TSession は外部から受け取る）
- ユースケース（ITransactionManager によるトランザクション制御）
- トランザクション管理（DB のトランザクション API を使用）
- ユーティリティ（Optional, ValidateHelper）
- 例外
- End-to-End 例
- 非推奨 API

コア概念とレイヤリング
- ドメイン層: Entity, Value Object, Domain Service, Repository, Aggregate Service を定義
- ユースケース層: トランザクションや複数集約操作を編成
- インフラ層: IRepository, ITransactionService などを実装
- 設計ノート: AggregateService/DomainService/UseCase は TSession のみをオープンにし、DI 登録時にクローズする。Repository と OperateInfo の型は集約サービスで確定させる。

エンティティ
- 本ツールキットでの要件
  - プロパティには可能な限り値オブジェクトを使用（プリミティブ直書きは避ける）
  - コンストラクタは非公開。Create（検証あり）と Reconstruct（検証なし）を提供
  - 値の更新は常に新しいインスタンスを返す（イミュータブル）
- IEntity<TIdentifier>
  - 強い型付けの識別子を持つエンティティの契約
  - メンバー: Identifier, Validate(), IsInvalidValue
- EntityBase<TSelf, TIdentifier>
  - Identifier による等価性、Validate に基づく IsInvalidValue を提供

例（値オブジェクトをプロパティに使い、ファクトリで生成）
```csharp
public sealed record UserId(Guid Value);

public sealed record UserName : ValueObjectBase,
    ISingleValueObject<string, UserName>, ILengthDefinedSingleValueObject
{
    public string Value { get; }
    public static int MinLength => 1;
    public static int MaxLength => 50;

    private UserName(string value) => Value = value;

    public static UserName Create(string value)
    {
        var vo = new UserName(value);
        vo.Validate();
        return vo;
    }

    public static UserName Reconstruct(string value) => new(value);

    public override void Validate()
    {
        var v = new ValidateHelper(); // 単一値 VO: ValidateHelper を使用
        v.AddNullCheck(Value, "Name is required.");
        v.AddStrLengthCheck(Value, MinLength, MaxLength, "Name length out of range.");
        v.ExecuteValidateWithThrowException();
    }
}

public sealed class User : EntityBase<User, UserId>
{
    public override UserId Identifier { get; }
    public UserName Name { get; }

    private User(UserId id, UserName name)
    {
        Identifier = id;
        Name = name;
    }

    public static User Create(UserId id, UserName name) // 検証あり
    {
        var entity = new User(id, name);
        entity.Validate();
        return entity;
    }

    public static User Reconstruct(UserId id, UserName name) => new(id, name); // 検証なし

    public User WithName(UserName newName) // イミュータブル更新
    {
        var updated = new User(Identifier, newName);
        updated.Validate();
        return updated;
    }

    public override void Validate()
    {
        var v = new KeyedValidateHelper<string>(); // エンティティ: KeyedValidateHelper
        v.Add("Id", () => { if (Identifier.Value == Guid.Empty) throw new ValueObjectInvalidException("Id empty"); });
        v.Add("Name", Name.Validate);
        v.ExecuteValidateWithThrowException();
    }
}
```

リポジトリ
- IRepository<TEntity, TEntityIdentifier, TOperateInfo, TSession>
  - FindByIdentifierAsync: Optional<TEntity> を返す
  - SaveAsync: 操作情報・トランザクションセッションを伴って永続化
- ドメインサービスやユースケースから直接触らず、必ず集約サービス経由でアクセス

集約サービス（AggregateService = 集約ルート）
- 目的: エンティティとリポジトリを内包し、集約内で完結する操作（存在確認、生成ルール、更新など）を提供
- ポリシー: 集約サービスは TSession のみを外から受け取り、Repository と OperateInfo は型を確定させる
- 共通取得処理などは AggregateServiceBase を継承

例（TSession のみオープン）
```csharp
public sealed class UserAggregateService<TSession> :
    AggregateServiceBase<User, UserId, IRepository<User, UserId, string, TSession>, string, TSession>
    where TSession : IDisposable
{
    public UserAggregateService(IRepository<User, UserId, string, TSession> repo) : base(repo) { }

    public async ValueTask<User> CreateAsync(TSession session, UserId id, UserName name, string op, CancellationToken ct)
    {
        var exists = await Repository.FindByIdentifierAsync(session, id, ct);
        if (exists.HasValue) throw new ObjectAlreadyExistException();
        var user = User.Create(id, name);
        await Repository.SaveAsync(session, user, op, ct);
        return user;
    }

    public async ValueTask<User> UpdateNameAsync(TSession session, UserId id, UserName newName, string op, CancellationToken ct)
    {
        var found = await Repository.FindByIdentifierAsync(session, id, ct);
        var user = found.GetValue(defaultValue: null!) ?? throw new ObjectNotFoundException();
        var updated = user.WithName(newName);
        await Repository.SaveAsync(session, updated, op, ct);
        return updated;
    }
}
```

ドメインサービス
- 複数集約にまたがる処理を担当。TSession は外部（ユースケース）から受け取る
- リポジトリへ直接アクセスせず、必ず集約サービスを介して操作
- ポリシー: ドメインサービスは TSession のみをオープンにし、依存する集約サービスで Repository/OperateInfo 型を確定

例（TSession のみオープン）
```csharp
public sealed record RegisterAndGreetReq<TSession>(TSession Session, UserId Id, UserName Name, string Op)
    : IDomainServiceDTO
    where TSession : IDisposable;

public sealed record RegisterAndGreetRes(string Message) : IDomainServiceDTO;

public sealed class RegisterAndGreetService<TSession>(
    UserAggregateService<TSession> users,
    GreeterAggregateService<TSession> greeter)
    : IDomainService<RegisterAndGreetReq<TSession>, RegisterAndGreetRes>
    where TSession : IDisposable
{
    private readonly UserAggregateService<TSession> _users = users;
    private readonly GreeterAggregateService<TSession> _greeter = greeter;

    public async ValueTask<RegisterAndGreetRes> ExecuteAsync(
        RegisterAndGreetReq<TSession> req,
        CancellationToken ct = default)
    {
        var user = await _users.CreateAsync(req.Session, req.Id, req.Name, req.Op, ct);
        var msg = await _greeter.ComposeGreetingAsync(req.Session, user, req.Op, ct);
        return new RegisterAndGreetRes(msg);
    }
}
```

ユースケース
- ITransactionManager を用いてトランザクションを制御。ユースケースがドメインへアクセスする窓口は DomainService のみ
- TSession は DI によりクローズ

例（TSession を DI でクローズ）
```csharp
public sealed record RegisterUserCommand(UserId Id, string Name) : ICommandServiceDTO;
public sealed record RegisterUserResult(Guid Id) : ICommandServiceDTO;

public sealed class RegisterUserCommandService<TSession> :
    ICommandService<RegisterUserCommand, RegisterUserResult>
    where TSession : IDisposable
{
    private readonly ITransactionManager _tx;
    private readonly RegisterAndGreetService<TSession> _domain;

    public RegisterUserCommandService(ITransactionManager tx, RegisterAndGreetService<TSession> domain)
    { _tx = tx; _domain = domain; }

    public async ValueTask<RegisterUserResult> ExecuteAsync(RegisterUserCommand req, CancellationToken ct = default)
    {
        await _tx.ExecuteTransactionAsync(
            sessionTypes: [ typeof(TSession) ],
            transactionFunction: async sessions =>
            {
                var session = sessions.GetSession<TSession>();
                var _ = await _domain.ExecuteAsync(new RegisterAndGreetReq<TSession>(session, req.Id, UserName.Create(req.Name), "register"), ct);
            });

        return new RegisterUserResult(req.Id.Value);
    }
}
```

トランザクション管理
- ITransactionService<TSession> は DB のトランザクション API を用いて実装（EF Core の場合は DbContext.Database.BeginTransaction/Rollback/Commit を使用）

例（EF Core）
```csharp
public sealed class EfSession : IDisposable
{
    public required MyDbContext Db { get; init; }
    public required IDbContextTransaction Tx { get; init; }
    public void Dispose() { Tx.Dispose(); Db.Dispose(); }
}

public sealed class EfTransactionService : ITransactionService<EfSession>
{
    private readonly IDbContextFactory<MyDbContext> _factory;
    public EfTransactionService(IDbContextFactory<MyDbContext> factory) => _factory = factory;

    public async ValueTask<EfSession> BeginAsync()
    {
        var db = await _factory.CreateDbContextAsync();
        var tx = await db.Database.BeginTransactionAsync();
        return new EfSession { Db = db, Tx = tx };
    }

    public async ValueTask CommitAsync(EfSession s)
    {
        await s.Db.SaveChangesAsync();
        await s.Tx.CommitAsync();
    }

    public async ValueTask RollbackAsync(EfSession s)
    {
        await s.Tx.RollbackAsync();
    }
}
```

ユーティリティ
- Optional<T>
  - 三値コンテナ: None/Some(null)/Some(value)
- ValidateHelper / KeyedValidateHelper<TKey>
  - 単一値 VO: ValidateHelper を推奨
  - エンティティ・複数値 VO: KeyedValidateHelper でキーごとに検証理由を集約

例外
- 基底: TADAException
- ドメイン: DomainInvalidOperationException
- 値オブジェクト: ValueObjectInvalidException, ValueObjectNullException, ValueObjectLengthException
- ライフサイクル: ObjectNotFoundException, ObjectAlreadyExistException
- 集約: MultiReasonException, KeyedMultiReasonException<TKey>

End-to-End 例（DI 構成、TSession をクローズ）
```csharp
// ここで TSession をクローズ
services.AddScoped<ITransactionService<EfSession>, EfTransactionService>();
services.AddScoped<IRepository<User, UserId, string, EfSession>, EfUserRepository>();
services.AddScoped<UserAggregateService<EfSession>>();
services.AddScoped<GreeterAggregateService<EfSession>>();
services.AddScoped<RegisterAndGreetService<EfSession>>();
services.AddScoped<ICommandService<RegisterUserCommand, RegisterUserResult>, RegisterUserCommandService<EfSession>>();
```

非推奨 API
- IDomainServiceWithRes<TReq, TRes>, ICommandServiceWithRes<TReq, TRes>, IQueryServiceWithoutReq<TRes>
  - 非推奨です。現行のジェネリック インターフェースを使用してください。
