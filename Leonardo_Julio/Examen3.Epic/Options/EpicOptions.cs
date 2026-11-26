namespace Examen3.Epic.Options;

public sealed class EpicOptions
{
    public const string SectionName = "Epic";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Deployment ID de Epic Online Services. Es opcional para el login web,
    /// pero algunos clientes de Epic lo exigen al canjear el código.
    /// </summary>
    public string DeploymentId { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = "http://localhost:5170/api/auth/epic/callback";

    public string Scope { get; set; } = "basic_profile";

    public string AuthorizationEndpoint { get; set; } = "https://www.epicgames.com/id/authorize";

    public string TokenEndpoint { get; set; } = "https://api.epicgames.dev/epic/oauth/v2/token";

    public string UserInfoEndpoint { get; set; } = "https://api.epicgames.dev/epic/oauth/v2/userInfo";

    public string RevokeEndpoint { get; set; } = "https://api.epicgames.dev/epic/oauth/v2/revoke";

    public bool IsConfigured =>
        IsRealValue(ClientId) &&
        IsRealValue(ClientSecret) &&
        IsValidRedirectUri(RedirectUri);

    public bool HasDeploymentId => IsRealValue(DeploymentId);

    public string EffectiveScope =>
        string.IsNullOrWhiteSpace(Scope) ? "basic_profile" : Scope.Trim();

    private static bool IsValidRedirectUri(string value) =>
        IsRealValue(value) &&
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool IsRealValue(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Contains("TU_", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains("REPLACE", StringComparison.OrdinalIgnoreCase);
}
