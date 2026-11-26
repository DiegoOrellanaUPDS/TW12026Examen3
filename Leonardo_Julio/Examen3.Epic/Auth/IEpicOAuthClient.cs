namespace Examen3.Epic.Auth;

public interface IEpicOAuthClient
{
    string BuildAuthorizeUrl(string state);

    Task<EpicUser> ExchangeCodeAsync(string code, CancellationToken cancellationToken);
}
