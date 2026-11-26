namespace Examen_3.Services;

/// <summary>
/// Centraliza la lectura, validación y limpieza de la sesión OAuth de Epic Games.
/// </summary>
public static class EpicSessionHelper
{
    public const string StateSessionKey = "EpicOAuthState";
    public const string ReturnUrlSessionKey = "EpicOAuthReturnUrl";
    public const string AccessTokenSessionKey = "EpicAccessToken";
    public const string TokenExpirySessionKey = "EpicTokenExpiry";

    public static (bool IsAuthenticated, DateTimeOffset? ExpiresAt) GetStatus(ISession session)
    {
        var accessToken = session.GetString(AccessTokenSessionKey);
        var expiryValue = session.GetString(TokenExpirySessionKey);
        var hasExpiry = DateTimeOffset.TryParse(expiryValue, out var expiresAt);

        var isAuthorized = !string.IsNullOrWhiteSpace(accessToken) &&
            hasExpiry &&
            expiresAt > DateTimeOffset.UtcNow;

        if (!isAuthorized)
        {
            session.Remove(AccessTokenSessionKey);
            session.Remove(TokenExpirySessionKey);
            return (false, null);
        }

        return (true, expiresAt);
    }

    public static void SetToken(ISession session, string accessToken, DateTimeOffset expiresAt)
    {
        session.SetString(AccessTokenSessionKey, accessToken);
        session.SetString(TokenExpirySessionKey, expiresAt.ToString("O"));
    }

    public static void Clear(ISession session)
    {
        session.Remove(AccessTokenSessionKey);
        session.Remove(TokenExpirySessionKey);
        session.Remove(StateSessionKey);
        session.Remove(ReturnUrlSessionKey);
    }
}
