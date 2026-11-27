using WebBanking.Contracts.Auth;
using WebBanking.Contracts.Common;

namespace WebBanking.Client.Services.Interfaces;

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
