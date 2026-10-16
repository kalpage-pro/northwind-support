namespace SupportDesk.Application.Abstractions;

public interface ICurrentUser
{
    int AgentId { get; }

    string FullName { get; }

    string Email { get; }
}
