namespace CSStack.TADA
{
	/// <summary>
	/// DTO interface used by query service methods. A marker: it keeps the three service families from
	/// accepting each other's DTOs by accident, and carries no members.
	/// </summary>
	/// <remarks>
	/// Declare these as <c>record</c>s. A query response is shaped for the caller that asked for it, so it
	/// holds plain data — not entities. Returning an entity from a query service would hand out a write
	/// model that has already left its transaction.
	/// </remarks>
	public interface IQueryServiceDTO;
}
