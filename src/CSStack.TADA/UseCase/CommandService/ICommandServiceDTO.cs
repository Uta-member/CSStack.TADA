namespace CSStack.TADA
{
	/// <summary>
	/// DTO interface used by use case command methods. A marker: it keeps the three service families from
	/// accepting each other's DTOs by accident, and carries no members.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Declare these as <c>record</c>s nested in the interface of the use case that takes them, named
	/// <c>Req</c> and <c>Res</c>.</b> Deriving that interface from <see cref="ICommandService{TReq}"/> or
	/// <see cref="ICommandService{TReq, TRes}"/> already fixes exactly one request type — and at most one
	/// response type — per use case, so the DTOs stand one to one with the interface. Nesting them puts
	/// each one where the interface leads (<c>ICreateUserCommandService.Req</c>) instead of in a flat
	/// namespace of <c>...Req</c> records that have to be found by name, and where handing another use
	/// case's request to this one still compiles.
	/// </para>
	/// <para>
	/// A command service DTO stands on the boundary of the application, so it holds the arguments the
	/// caller supplied and the operation info to record with the write — not entities, and not a
	/// transaction session: the command service starts the transaction itself. Converting the arguments
	/// into value objects is the command service's job, and an exception from that conversion — thrown by
	/// <c>Create</c> — is how invalid input is reported.
	/// </para>
	/// </remarks>
	public interface ICommandServiceDTO;
}
