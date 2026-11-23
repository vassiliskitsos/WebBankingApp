using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using WebBanking.Contracts.Auth;
using WebBanking.Contracts.Common;

namespace WebBanking.Client.Services;

public interface IApiClient
{
    Task<Result<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TResponse>> GetAsync<TResponse>(
        string uri,
        bool requiresAuth = true,
        CancellationToken cancellationToken = default);

    Task<Result<TResponse>> PostAsync<TRequest, TResponse>(
        string uri,
        TRequest payload,
        bool requiresAuth = true,
        CancellationToken cancellationToken = default);

    Task<Result> PostAsync<TRequest>(
        string uri,
        TRequest payload,
        bool requiresAuth = true,
        CancellationToken cancellationToken = default);
}

public sealed class ApiClient(
    HttpClient httpClient,
    IAuthTokenStore tokenStore,
    NavigationManager navigation,
    JwtAuthenticationStateProvider? authStateProvider = null) : IApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await PostAsync<LoginRequest, AuthResponse>(
            "api/auth/login",
            request,
            requiresAuth: false,
            cancellationToken);

        if (result.IsSuccess && result.Value is not null)
        {
            await tokenStore.SetSessionAsync(result.Value);
        }

        return result;
    }

    public Task<Result<TResponse>> GetAsync<TResponse>(
        string uri,
        bool requiresAuth = true,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<TResponse>(
            () => new HttpRequestMessage(HttpMethod.Get, uri),
            requiresAuth,
            cancellationToken);
    }

    public Task<Result<TResponse>> PostAsync<TRequest, TResponse>(
        string uri,
        TRequest payload,
        bool requiresAuth = true,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<TResponse>(
            () => new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            },
            requiresAuth,
            cancellationToken);
    }

    public async Task<Result> PostAsync<TRequest>(
        string uri,
        TRequest payload,
        bool requiresAuth = true,
        CancellationToken cancellationToken = default)
    {
        var response = await SendRawAsync(
            () => new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            },
            requiresAuth,
            cancellationToken);

        if (!response.IsSuccess)
        {
            return Result.Fail(response.Error!);
        }

        using var _ = response.Value;
        return Result.Ok();
    }

    private async Task<Result<TResponse>> SendAsync<TResponse>(
        Func<HttpRequestMessage> requestFactory,
        bool requiresAuth,
        CancellationToken cancellationToken)
    {
        var rawResult = await SendRawAsync(
            requestFactory,
            requiresAuth,
            cancellationToken);

        if (!rawResult.IsSuccess)
        {
            return Result<TResponse>.Fail(rawResult.Error!);
        }

        using var response = rawResult.Value!;

        try
        {
            var data = await response.Content.ReadFromJsonAsync<TResponse>(
                JsonOptions,
                cancellationToken);

            if (data is null)
            {
                return Result<TResponse>.Fail(
                    new Error(
                        ErrorType.Unprocessable,
                        "Η απόκριση του διακομιστή ήταν κενή."));
            }

            return Result<TResponse>.Ok(data);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return Result<TResponse>.Fail(
                new Error(
                    ErrorType.Unprocessable,
                    "Μη έγκυρη μορφή δεδομένων από τον διακομιστή."));
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            return Result<TResponse>.Fail(
                new Error(
                    ErrorType.Unprocessable,
                    "Η σύνδεση με τον διακομιστή διακόπηκε."));
        }
    }

    private async Task<Result<HttpResponseMessage>> SendRawAsync(
        Func<HttpRequestMessage> requestFactory,
        bool requiresAuth,
        CancellationToken cancellationToken)
    {
        using var request = requestFactory();
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        string? sentToken = null;

        if (requiresAuth)
        {
            await tokenStore.InitializeAsync();

            sentToken = tokenStore.Token;
            if (string.IsNullOrWhiteSpace(sentToken))
            {
                await HandleUnauthorizedAsync(sentToken: null);
                return Result<HttpResponseMessage>.Fail(
                    new Error(
                        ErrorType.Unauthorized,
                        "Η συνεδρία σας έχει λήξει. Παρακαλούμε συνδεθείτε ξανά."));
            }

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", sentToken);
        }

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Result<HttpResponseMessage>.Fail(
                new Error(
                    ErrorType.Unprocessable,
                    "Αδυναμία επικοινωνίας με τον διακομιστή. Ελέγξτε τη σύνδεσή σας."));
        }

        if (response.IsSuccessStatusCode)
        {
            return Result<HttpResponseMessage>.Ok(response);
        }

        using (response)
        {
            var error = await MapErrorAsync(response, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized && requiresAuth)
            {
                await HandleUnauthorizedAsync(sentToken);
            }

            return Result<HttpResponseMessage>.Fail(error);
        }
    }

    private async Task HandleUnauthorizedAsync(string? sentToken)
    {
        await tokenStore.InitializeAsync();

        var currentToken = tokenStore.Token;
        var hasNewerSession =
            !string.IsNullOrWhiteSpace(currentToken) &&
            !string.Equals(currentToken, sentToken, StringComparison.Ordinal);

        if (hasNewerSession)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(currentToken))
        {
            await tokenStore.ClearAsync();
        }

        authStateProvider?.NotifyAuthStateChanged();

        var currentPath = navigation.ToBaseRelativePath(navigation.Uri);
        if (!currentPath.StartsWith("login", StringComparison.OrdinalIgnoreCase))
        {
            navigation.NavigateTo("/login");
        }
    }

    private static async Task<Error> MapErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var errorType = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => ErrorType.Validation,
            HttpStatusCode.Unauthorized => ErrorType.Unauthorized,
            HttpStatusCode.NotFound => ErrorType.NotFound,
            HttpStatusCode.Conflict => ErrorType.Conflict,
            _ => ErrorType.Unprocessable
        };

        string? message = null;

        try
        {
            var payload = await response.Content.ReadFromJsonAsync<ApiErrorPayload>(
                JsonOptions,
                cancellationToken);

            message = payload?.Message ?? payload?.Title;
        }
        catch (Exception)
        {
            // Fallback to default status message when body is empty or non-JSON.
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Λάθος όνομα χρήστη ή κωδικός πρόσβασης.",
                HttpStatusCode.BadRequest => "Μη έγκυρα στοιχεία αιτήματος.",
                HttpStatusCode.NotFound => "Ο πόρος δεν βρέθηκε.",
                HttpStatusCode.Conflict => "Υπάρχει σύγκρουση δεδομένων.",
                _ => "Παρουσιάστηκε απρόσμενο σφάλμα στον διακομιστή."
            };
        }

        return new Error(errorType, message);
    }

    private sealed record ApiErrorPayload(string? Message, string? Title);
}
