using Microsoft.AspNetCore.Identity;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Infrastructure.Services;

public sealed class PasswordHasherService : IPasswordHasher
{
    private readonly PasswordHasher<object> hasher = new();

    public string Hash(string password) => hasher.HashPassword(new object(), password);

    public bool Verify(string hash, string password) =>
        hasher.VerifyHashedPassword(new object(), hash, password) is PasswordVerificationResult.Success;
}
