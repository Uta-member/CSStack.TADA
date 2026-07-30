namespace CSStack.TADA
{
	/// <summary>
	/// DTO interface used by query service methods. A marker: it keeps the three service families from
	/// accepting each other's DTOs by accident, and carries no members.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Declare these as <c>record</c>s nested in the interface of the query that uses them, named
	/// <c>Req</c> and <c>Res</c></b> — see <see cref="ICommandServiceDTO"/> for why. A read model shared by
	/// several queries (a row type appearing in more than one response) is not one of these and stays
	/// outside; only the types that stand one to one with the query are nested.
	/// </para>
	/// <para>
	/// A query response is shaped for the caller that asked for it, so it holds plain data — not entities.
	/// Returning an entity from a query service would hand out a write model that has already left its
	/// transaction.
	/// </para>
	/// </remarks>
	public interface IQueryServiceDTO;
}
