using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Examen3.Epic.Auth;

/// <summary>
/// Sesiones de la aplicación guardadas en memoria. La cookie solo contiene el
/// identificador de sesión; al cerrar sesión se elimina del servidor y una
/// cookie copiada deja de ser válida.
/// </summary>
public sealed class InMemoryAppSessionStore : IAppSessionStore
{
    private readonly ConcurrentDictionary<string, SessionRecord> _sessions =
        new(StringComparer.Ordinal);

    private readonly TimeProvider _timeProvider;

    public InMemoryAppSessionStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public string Create(string userId, string provider, string displayName, TimeSpan lifetime)
    {
        CleanupExpired();

        var sessionId = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        _sessions[sessionId] = new SessionRecord(
            userId,
            provider,
            displayName,
            _timeProvider.GetUtcNow().Add(lifetime));

        return sessionId;
    }

    public bool IsActive(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var session) &&
        session.ExpiresAt > _timeProvider.GetUtcNow();

    public void Revoke(string sessionId) => _sessions.TryRemove(sessionId, out _);

    private void CleanupExpired()
    {
        var now = _timeProvider.GetUtcNow();

        foreach (var pair in _sessions)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                _sessions.TryRemove(pair.Key, out _);
            }
        }
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private sealed record SessionRecord(
        string UserId,
        string Provider,
        string DisplayName,
        DateTimeOffset ExpiresAt);
}
