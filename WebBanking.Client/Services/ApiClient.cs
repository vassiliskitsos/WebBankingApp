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
    NavigationManager navigationManager,
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

    public async Task<Result<TResponse>> GetAsync<TResponse>(
        string uri,
        bool requiresAuth = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (request, sentToken) = await CreateRequestAsync(
                HttpMethod.Get,
                uri,
                requiresAuth);

            using (request)
            {
                using var response = await httpClient.SendAsync(request, cancellationToken);
                return await HandleTypedResponseAsync<TResponse>(
                    response,
                    sentToken,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<TResponse>.Fail(
                new Error(ErrorType.Unexpected, "Το αίτημα ακυρώθηκε."));
        }
        catch (HttpRequestException)
        {
            return Result<TResponse>.Fail(
                new Error(ErrorType.Unexpected, "Αδυναμία επικοινωνίας με τον διακομιστή. Ελέγξτε τη σύνδεσή σας."));
        }
        catch (Exception)
        {
            return Result<TResponse>.Fail(
                new Error(ErrorType.Unexpected, "Παρουσιάστηκε απρόσμενο σφάλμα επικοινωνίας."));
        }
    }

    public async Task<Result<TResponse>> PostAsync<TRequest, TResponse>(
        string uri,
        TRequest payload,
        bool requiresAuth = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (request, sentToken) = await CreateRequestAsync(
                HttpMethod.Post,
                uri,
                requiresAuth);

            using (request)
            {
                request.Content = JsonContent.Create(payload, options: JsonOptions);

                using var response = await httpClient.SendAsync(request, cancellationToken);
                return await HandleTypedResponseAsync<TResponse>(
                    response,
                    sentToken,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<TResponse>.Fail(
                new Error(ErrorType.Unexpected, "Το αίτημα ακυρώθηκε."));
        }
        catch (HttpRequestException)
        {
            return Result<TResponse>.Fail(
                new Error(ErrorType.Unexpected, "Αδυναμία επικοινωνίας με τον διακομιστή. Ελέγξτε τη σύνδεσή σας."));
        }
        catch (Exception)
        {
            return Result<TResponse>.Fail(
                new Error(ErrorType.Unexpected, "Παρουσιάστηκε απρόσμενο σφάλμα επικοινωνίας."));
        }
    }

    public async Task<Result> PostAsync<TRequest>(
        string uri,
        TRequest payload,
        bool requiresAuth = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (request, sentToken) = await CreateRequestAsync(
                HttpMethod.Post,
                uri,
                requiresAuth);

            using (request)
            {
                request.Content = JsonContent.Create(payload, options: JsonOptions);

                using var response = await httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return Result.Success();
                }

                var error = await HandleErrorResponseAsync(
                    response,
                    sentToken,
                    cancellationToken);
                return Result.Fail(error);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Fail(
                new Error(ErrorType.Unexpected, "Το αίτημα ακυρώθηκε."));
        }
        catch (HttpRequestException)
        {
            return Result.Fail(
                new Error(ErrorType.Unexpected, "Αδυναμία επικοινωνίας με τον διακομιστή. Ελέγξτε τη σύνδεσή σας."));
        }
        catch (Exception)
        {
            return Result.Fail(
                new Error(ErrorType.Unexpected, "Παρουσιάστηκε απρόσμενο σφάλμα επικοινωνίας."));
        }
    }

    private async Task<(HttpRequestMessage Request, string? SentToken)> CreateRequestAsync(
        HttpMethod method,
        string uri,
        bool requiresAuth)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        string? sentToken = null;

        if (requiresAuth)
        {
            await tokenStore.InitializeAsync();
            sentToken = tokenStore.Token;

            if (!string.IsNullOrWhiteSpace(sentToken))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", sentToken);
            }
        }

        return (request, sentToken);
    }

    private async Task<Result<TResponse>> HandleTypedResponseAsync<TResponse>(
        HttpResponseMessage response,
        string? sentToken,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            try
            {
                var data = await response.Content.ReadFromJsonAsync<TResponse>(
                    JsonOptions,
                    cancellationToken);

                if (data is null)
                {
                    return Result<TResponse>.Fail(
                        new Error(ErrorType.Unexpected, "Κενή απάντηση από τον διακομιστή."));
                }

                return Result<TResponse>.Success(data);
            }
            catch (JsonException)
            {
                return Result<TResponse>.Fail(
                    new Error(ErrorType.Unexpected, "Μη έγκυρη μορφή δεδομένων από τον διακομιστή."));
            }
        }

        var error = await HandleErrorResponseAsync(
            response,
            sentToken,
            cancellationToken);
        return Result<TResponse>.Fail(error);
    }

    private async Task<Error> HandleErrorResponseAsync(
        HttpResponseMessage response,
        string? sentToken,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await tokenStore.InitializeAsync();

            var isSameActiveToken =
                !string.IsNullOrWhiteSpace(sentToken) &&
                string.Equals(tokenStore.Token, sentToken, StringComparison.Ordinal);

            if (isSameActiveToken || string.IsNullOrWhiteSpace(tokenStore.Token))
            {
                if (isSameActiveToken)
                {
                    await tokenStore.ClearAsync();
                    authStateProvider?.NotifyAuthStateChanged();
                }

                if (!navigationManager.Uri.EndsWith("/login", StringComparison.OrdinalIgnoreCase))
                {
                    navigationManager.NavigateTo("/login");
                }
            }
        }

        var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(rawBody))
        {
            try
            {
                var apiError = JsonSerializer.Deserialize<ApiError>(rawBody, JsonOptions);
                if (apiError is not null && !string.IsNullOrWhiteSpace(apiError.Message))
                {
                    return new Error(MapStatusCodeToErrorType(response.StatusCode), apiError.Message);
                }
            }
            catch (JsonException)
            {
                // Ignore non-JSON error payloads (e.g. HTML/plain text) and use fallback message.
            }
        }

        return new Error(
            MapStatusCodeToErrorType(response.StatusCode),
            GetFallbackErrorMessage(response.StatusCode));
    }

    private static ErrorType MapStatusCodeToErrorType(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.BadRequest => ErrorType.Validation,
            HttpStatusCode.Unauthorized => ErrorType.Unauthorized,
            HttpStatusCode.Forbidden => ErrorType.Forbidden,
            HttpStatusCode.NotFound => ErrorType.NotFound,
            HttpStatusCode.Conflict => ErrorType.Conflict,
            _ => ErrorType.Unexpected
        };

    private static string GetFallbackErrorMessage(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.BadRequest => "Μη έγκυρα στοιχεία αιτήματος.",
            HttpStatusCode.Unauthorized => "Η συνεδρία σας έληξε ή τα στοιχεία σύνδεσης είναι λανθασμένα.",
            HttpStatusCode.Forbidden => "Δεν έχετε δικαίωμα πρόσβασης.",
            HttpStatusCode.NotFound => "Ο ζητούμενος πόρος δεν βρέθηκε.",
            HttpStatusCode.Conflict => "Παρουσιάστηκε διένεξη δεδομένων.",
            _ => "Παρουσιάστηκε σφάλμα στον διακομιστή. Δοκιμάστε ξανά αργότερα."
        };
}
