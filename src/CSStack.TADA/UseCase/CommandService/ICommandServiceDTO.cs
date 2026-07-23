namespace CSStack.TADA
{
	/// <summary>
	/// DTO interface used by use case command methods. A marker: it keeps the three service families from
	/// accepting each other's DTOs by accident, and carries no members.
	/// </summary>
	/// <remarks>
	/// Declare these as <c>record</c>s. A command service DTO stands on the boundary of the application,
	/// so it holds the arguments the caller supplied and the operation info to record with the write — not
	/// entities, and not a transaction session: the command service starts the transaction itself.
	/// Converting the arguments into value objects is the command service's job, and
	/// <see cref="ValueObjectInvalidException"/> from that conversion is how invalid input is reported.
	/// </remarks>
	public interface ICommandServiceDTO;
}
