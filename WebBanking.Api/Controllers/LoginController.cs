using Microsoft.AspNetCore.Mvc;
using WebBanking.Api.Services.Interfaces;
using WebBanking.Contracts.Auth;
using WebBanking.Contracts.Common;

namespace WebBanking.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class LoginController(ILoginService loginService)
    : ControllerBase
{
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await loginService.LoginAsync(
            request,
            cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        var error = result.Error!;

        return error.Type switch
        {
            ErrorType.Unauthorized =>
                Unauthorized(new { error.Message }),

            ErrorType.Validation =>
                BadRequest(new { error.Message }),

            _ =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { Message = "An unexpected error occurred." })
        };
    }
}
