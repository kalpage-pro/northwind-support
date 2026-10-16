using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Login;
using SupportDesk.Application.Features.Auth.Me;

namespace SupportDesk.Presentation.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(
    LoginCommandHandler login,
    GetCurrentUserQueryHandler currentUser) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await login.HandleAsync(request, ct);
        if (!result.Succeeded || result.IssuedToken is null || result.User is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "Invalid email or password.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        return Ok(new
        {
            token = result.IssuedToken.Token,
            expiresAtUtc = result.IssuedToken.ExpiresAtUtc,
            user = result.User
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await currentUser.HandleAsync(ct);
        return result.Succeeded
            ? Ok(result.User)
            : Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "The authenticated agent is no longer active.",
                Status = StatusCodes.Status401Unauthorized
            });
    }
}
