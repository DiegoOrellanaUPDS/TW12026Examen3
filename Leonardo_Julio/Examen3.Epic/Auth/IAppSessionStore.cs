namespace Examen3.Epic.Auth;

public interface IAppSessionStore
{
    string Create(string userId, string provider, string displayName, TimeSpan lifetime);

    bool IsActive(string sessionId);

    void Revoke(string sessionId);
}
