# CSStack.TADA �g�����K�C�h�i���{��j

�{�h�L�������g�� CSStack.TADA �Ɋ܂܂��e�N���X�E�C���^�[�t�F�[�X�̎g������̌n�I���ڍׂɐ������܂��B�Ώۂ� .NET 8 / C# 12 �ł��B

�ڎ�
- �R�A�T�O�ƃ��C�������O
- �G���e�B�e�B�i�l�I�u�W�F�N�g�̎g�p�A����J�R���X�g���N�^�ACreate/Reconstruct�A�C�~���[�^�u���X�V�j
- �l�I�u�W�F�N�g�iValidateHelper/KeyedValidateHelper�j
- ���|�W�g��
- �W��T�[�r�X�iAggregateService = �W�񃋁[�g�ATSession �݂̂��O������󂯎��j
- �h���C���T�[�r�X�i�����W����܂����ETSession �͊O������󂯎��j
- ���[�X�P�[�X�iITransactionManager �ɂ��g�����U�N�V��������j
- �g�����U�N�V�����Ǘ��iDB �̃g�����U�N�V���� API ���g�p�j
- ���[�e�B���e�B�iOptional, ValidateHelper�j
- ��O
- End-to-End ��
- �񐄏� API

�R�A�T�O�ƃ��C�������O
- �h���C���w: Entity, Value Object, Domain Service, Repository, Aggregate Service ���`
- ���[�X�P�[�X�w: �g�����U�N�V�����╡���W�񑀍��Ґ�
- �C���t���w: IRepository, ITransactionService �Ȃǂ�����
- �݌v�m�[�g: AggregateService/DomainService/UseCase �� TSession �݂̂��I�[�v���ɂ��ADI �o�^���ɃN���[�Y����BRepository �� OperateInfo �̌^�͏W��T�[�r�X�Ŋm�肳����B

�G���e�B�e�B
- �{�c�[���L�b�g�ł̗v��
  - �v���p�e�B�ɂ͉\�Ȍ���l�I�u�W�F�N�g���g�p�i�v���~�e�B�u�������͔�����j
  - �R���X�g���N�^�͔���J�BCreate�i���؂���j�� Reconstruct�i���؂Ȃ��j���
  - �l�̍X�V�͏�ɐV�����C���X�^���X��Ԃ��i�C�~���[�^�u���j
- IEntity<TIdentifier>
  - �����^�t���̎��ʎq�����G���e�B�e�B�̌_��
  - �����o�[: Identifier, Validate(), IsInvalidValue
- EntityBase<TSelf, TIdentifier>
  - Identifier �ɂ�铙�����AValidate �Ɋ�Â� IsInvalidValue ���

��i�l�I�u�W�F�N�g���v���p�e�B�Ɏg���A�t�@�N�g���Ő����j
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
        var v = new ValidateHelper(); // �P��l VO: ValidateHelper ���g�p
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

    public static User Create(UserId id, UserName name) // ���؂���
    {
        var entity = new User(id, name);
        entity.Validate();
        return entity;
    }

    public static User Reconstruct(UserId id, UserName name) => new(id, name); // ���؂Ȃ�

    public User WithName(UserName newName) // �C�~���[�^�u���X�V
    {
        var updated = new User(Identifier, newName);
        updated.Validate();
        return updated;
    }

    public override void Validate()
    {
        var v = new KeyedValidateHelper<string>(); // �G���e�B�e�B: KeyedValidateHelper
        v.Add("Id", () => { if (Identifier.Value == Guid.Empty) throw new ValueObjectInvalidException("Id empty"); });
        v.Add("Name", Name.Validate);
        v.ExecuteValidateWithThrowException();
    }
}
```

���|�W�g��
- IRepository<TEntity, TEntityIdentifier, TOperateInfo, TSession>
  - FindByIdentifierAsync: Optional<TEntity> ��Ԃ�
  - SaveAsync: ������E�g�����U�N�V�����Z�b�V�����𔺂��ĉi����
- �h���C���T�[�r�X�⃆�[�X�P�[�X���璼�ڐG�炸�A�K���W��T�[�r�X�o�R�ŃA�N�Z�X

�W��T�[�r�X�iAggregateService = �W�񃋁[�g�j
- �ړI: �G���e�B�e�B�ƃ��|�W�g�������A�W����Ŋ������鑀��i���݊m�F�A�������[���A�X�V�Ȃǁj���
- �|���V�[: �W��T�[�r�X�� TSession �݂̂��O����󂯎��ARepository �� OperateInfo �͌^���m�肳����
- ���ʎ擾�����Ȃǂ� AggregateServiceBase ���p��

��iTSession �̂݃I�[�v���j
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

�h���C���T�[�r�X
- �����W��ɂ܂����鏈����S���BTSession �͊O���i���[�X�P�[�X�j����󂯎��
- ���|�W�g���֒��ڃA�N�Z�X�����A�K���W��T�[�r�X����đ���
- �|���V�[: �h���C���T�[�r�X�� TSession �݂̂��I�[�v���ɂ��A�ˑ�����W��T�[�r�X�� Repository/OperateInfo �^���m��

��iTSession �̂݃I�[�v���j
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

���[�X�P�[�X
- ITransactionManager ��p���ăg�����U�N�V�����𐧌�B���[�X�P�[�X���h���C���փA�N�Z�X���鑋���� DomainService �̂�
- TSession �� DI �ɂ��N���[�Y

��iTSession �� DI �ŃN���[�Y�j
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

�g�����U�N�V�����Ǘ�
- ITransactionService<TSession> �� DB �̃g�����U�N�V���� API ��p���Ď����iEF Core �̏ꍇ�� DbContext.Database.BeginTransaction/Rollback/Commit ���g�p�j

��iEF Core�j
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

���[�e�B���e�B
- Optional<T>
  - �O�l�R���e�i: None/Some(null)/Some(value)
- ValidateHelper / KeyedValidateHelper<TKey>
  - �P��l VO: ValidateHelper �𐄏�
  - �G���e�B�e�B�E�����l VO: KeyedValidateHelper �ŃL�[���ƂɌ��ؗ��R���W��

��O
- ���: TADAException
- �h���C��: DomainInvalidOperationException
- �l�I�u�W�F�N�g: ValueObjectInvalidException, ValueObjectNullException, ValueObjectLengthException
- ���C�t�T�C�N��: ObjectNotFoundException, ObjectAlreadyExistException
- �W��: MultiReasonException, KeyedMultiReasonException<TKey>

End-to-End ��iDI �\���ATSession ���N���[�Y�j
```csharp
// ������ TSession ���N���[�Y
services.AddScoped<ITransactionService<EfSession>, EfTransactionService>();
services.AddScoped<IRepository<User, UserId, string, EfSession>, EfUserRepository>();
services.AddScoped<UserAggregateService<EfSession>>();
services.AddScoped<GreeterAggregateService<EfSession>>();
services.AddScoped<RegisterAndGreetService<EfSession>>();
services.AddScoped<ICommandService<RegisterUserCommand, RegisterUserResult>, RegisterUserCommandService<EfSession>>();
```

�񐄏� API
- IDomainServiceWithRes<TReq, TRes>, ICommandServiceWithRes<TReq, TRes>, IQueryServiceWithoutReq<TRes>
  - �񐄏��ł��B���s�̃W�F�l���b�N �C���^�[�t�F�[�X���g�p���Ă��������B
