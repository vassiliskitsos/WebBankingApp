using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using WebBanking.Api.Data.Entities;
using WebBanking.Api.Repositories;
using WebBanking.Contracts.Auth;
using WebBanking.Contracts.Common;

namespace WebBanking.Api.Services;

public sealed class RegistrationService(
    IUserRepository users,
    IPasswordHasher<User> passwordHasher)
    : IRegistrationService
{
    public async Task<Result<int>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Validate the existing RegisterRequest contract.
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        if (!isValid)
        {
            var message = string.Join(
                " ",
                validationResults.Select(v => v.ErrorMessage));

            return Result<int>.Fail(
                new Error(ErrorType.Validation, message));
        }

        // Early uniqueness check for a useful error response.
        if (await users.UsernameExistsAsync(
            request.Username,
            cancellationToken))
        {
            return UsernameConflict();
        }

        // Never store the plaintext password.
        var user = new User
        {
            Username = request.Username,
            Role = Role.Customer
        };

        user.PasswordHash = passwordHasher.HashPassword(
            user,
            request.Password);

        try
        {
            var userId = await users.CreateAsync(
                user,
                cancellationToken);

            return Result<int>.Ok(userId);
        }
        catch (DuplicateUsernameException)
        {
            // Handles the race where two requests checked
            // the same username before either saved it.
            return UsernameConflict();
        }
    }

    private static Result<int> UsernameConflict()
    {
        return Result<int>.Fail(
            new Error(
                ErrorType.Conflict,
                "Το όνομα χρήστη χρησιμοποιείται ήδη."));
    }
}
