namespace CSStack.TADA
{
	/// <summary>
	/// DTO interface used by domain service methods. A marker: it keeps the three service families from
	/// accepting each other's DTOs by accident, and carries no members.
	/// </summary>
	/// <remarks>
	/// Declare these as <c>record</c>s. Unlike the DTOs of the other two families, a domain service DTO
	/// normally carries the transaction session as well as the arguments, because
	/// <see cref="IDomainService{TReq}.ExecuteAsync"/> has no session parameter — see
	/// <see cref="IDomainService{TReq}"/>. It may hold entities and value objects: a domain service runs
	/// inside the domain, so nothing has to be flattened on the way in.
	/// </remarks>
	public interface IDomainServiceDTO;
}
