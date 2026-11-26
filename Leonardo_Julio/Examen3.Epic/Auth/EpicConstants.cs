namespace Examen3.Epic.Auth;

public static class EpicConstants
{
    public const string Provider = "Epic";
    public const string ProviderDisplayName = "Epic Games";

    public const string ProviderClaim = "provider";
    public const string SessionIdClaim = "app_session_id";
    public const string AvatarClaim = "avatar_url";
    public const string PreferredUsernameClaim = "preferred_username";

    public const string EpicPolicy = "EpicUser";

    public const string SessionCookieName = "examen3.auth";
    public const string StateCookieName = "examen3.epic.oauth_state";
    public const string StateCookiePath = "/api/auth/epic";

    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    public static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
}
