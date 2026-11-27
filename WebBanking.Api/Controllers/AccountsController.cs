using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebBanking.Api.Data.Entities;
using WebBanking.Api.Services.Interfaces;
using WebBanking.Contracts.Accounts;
using WebBanking.Contracts.Common;

namespace WebBanking.Api.Controllers;

[ApiController]
[Route("api/accounts")]
[Authorize(Roles = nameof(Role.Customer))]
public sealed class AccountsController(IAccountService accountService)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AccountDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAccounts(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await accountService.GetAccountsAsync(
            userId,
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

            _ =>
                StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { Message = "An unexpected error occurred." })
        };
    }

    // The user id comes only from the JWT, never from the request.
    private bool TryGetUserId(out int userId)
    {
        return int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }
}
