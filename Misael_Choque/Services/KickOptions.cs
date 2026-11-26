namespace Examen_3.Services;

public class KickOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RedirectUri { get; set; } = "https://localhost:7010/auth/kick/callback";
    public string Scope { get; set; } = "user:read";
}
