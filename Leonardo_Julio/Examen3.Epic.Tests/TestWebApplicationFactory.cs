using Examen3.Epic.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Examen3.Epic.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public string NotesDirectory { get; } =
        Path.Combine(Path.GetTempPath(), "examen3-epic-tests", Guid.NewGuid().ToString("N"));

    public string NotesFilePath => Path.Combine(NotesDirectory, "notes.json");

    public FakeEpicOAuthClient EpicClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Epic:ClientId"] = "test-client-id",
                ["Epic:ClientSecret"] = "test-client-secret",
                ["Epic:DeploymentId"] = "test-deployment-id",
                ["Epic:RedirectUri"] = "http://localhost/api/auth/epic/callback",
                ["Epic:Scope"] = "basic_profile",
                ["Storage:NotesFile"] = NotesFilePath
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEpicOAuthClient>();
            services.AddSingleton<IEpicOAuthClient>(EpicClient);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        try
        {
            if (Directory.Exists(NotesDirectory))
            {
                Directory.Delete(NotesDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // La limpieza de archivos temporales no debe hacer fallar las pruebas.
        }
        catch (UnauthorizedAccessException)
        {
            // Igual que arriba: es solo un directorio temporal de pruebas.
        }
    }
}

public sealed class FakeEpicOAuthClient : IEpicOAuthClient
{
    public EpicUser User { get; set; } = new(
        "epic-account-123",
        "Leonardo Julio",
        "leonardo_julio",
        "https://cdn.example/avatar.png");

    public string? LastCode { get; private set; }

    public string BuildAuthorizeUrl(string state) =>
        "https://www.epicgames.com/id/authorize" +
        "?client_id=test-client-id" +
        "&response_type=code" +
        "&scope=basic_profile" +
        $"&redirect_uri={Uri.EscapeDataString("http://localhost/api/auth/epic/callback")}" +
        $"&state={Uri.EscapeDataString(state)}";

    public Task<EpicUser> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        LastCode = code;
        return Task.FromResult(User);
    }
}
