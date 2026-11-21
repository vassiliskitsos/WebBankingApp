using Microsoft.AspNetCore.Mvc;
using WebBanking.Api.Services;
using WebBanking.Contracts.Auth;
using WebBanking.Contracts.Common;

namespace WebBanking.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IRegistrationService registrationService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await registrationService.RegisterAsync(
            request,
            cancellationToken);

        if (result.IsSuccess)
        {
            return StatusCode(
                StatusCodes.Status201Created,
                new { userId = result.Value });
        }

        var error = result.Error!;

        return error.Type switch
        {
            ErrorType.Validation =>
                BadRequest(new { message = error.Message }),

            ErrorType.Conflict =>
                Conflict(new { message = error.Message }),

            _ =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { message = "An unexpected error occurred." })
        };
    }
}
