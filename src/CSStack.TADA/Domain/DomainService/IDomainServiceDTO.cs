namespace CSStack.TADA
{
	/// <summary>
	/// DTO interface used by domain service methods. A marker: it keeps the three service families from
	/// accepting each other's DTOs by accident, and carries no members.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Declare these as <c>record</c>s nested in the interface of the domain service that takes them,
	/// named <c>Req</c> and <c>Res</c></b> — see <see cref="ICommandServiceDTO"/> for why. It is true that
	/// <c>IDomainService&lt;TReq&gt;</c> is already keyed by this type, so a caller could depend on
	/// <c>IDomainService&lt;EnsureEmailIsUniqueReq&lt;TUserSession&gt;&gt;</c> without any further
	/// interface; declare one anyway
	/// (<c>IEmailUniquenessService&lt;TUserSession&gt; : IDomainService&lt;IEmailUniquenessService&lt;TUserSession&gt;.Req&gt;</c>),
	/// because that interface is what gives the DTO a home the reader can reach from the service and
	/// fixes the two to each other.
	/// </para>
	/// <para>
	/// Unlike the DTOs of the other two families, a domain service DTO normally carries the transaction
	/// session as well as the arguments, because <see cref="IDomainService{TReq}.ExecuteAsync"/> has no
	/// session parameter — see <see cref="IDomainService{TReq}"/>. It may hold entities and value objects:
	/// a domain service runs inside the domain, so nothing has to be flattened on the way in.
	/// </para>
	/// <para>
	/// The session it carries is a type parameter, not a concrete type — the type parameter of the
	/// enclosing interface, which because a domain service can span aggregates is named after the
	/// aggregate rather than called <c>TSession</c>; see
	/// <see cref="IRepository{TEntity, TEntityIdentifier, TOperateInfo, TSession}"/>. The DTOs of the
	/// other two families carry no session and therefore need no such type parameter.
	/// </para>
	/// </remarks>
	public interface IDomainServiceDTO;
}
