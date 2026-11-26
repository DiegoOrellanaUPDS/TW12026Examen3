using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Examen_3;
using Microsoft.AspNetCore.Mvc;

namespace MyApp.Namespace
{
    [Route("api/[controller]")]
    [ApiController]
    [RequiereSession]
    public class NotasNotionController : ControllerBase
    {
        private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public NotasNotionController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public sealed record CrearNotaRequest(string Titulo, string Contenido);

    [HttpPost]
    public async Task<IActionResult> CrearNota([FromBody] CrearNotaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Titulo))
            return BadRequest("El título es obligatorio.");

        var token = HttpContext.Session.GetString("NotionAccessToken")!;
        var parentPageId = _configuration["Notion:ParentPageId"]!;

        var client = CrearClienteNotion(token);

        var payload = new
        {
            parent = new { page_id = parentPageId },
            properties = new
            {
                title = new[]
                {
                    new
                    {
                        text = new { content = request.Titulo }
                    }
                }
            },
            children = string.IsNullOrWhiteSpace(request.Contenido)
                ? Array.Empty<object>()
                : new object[]
                {
                    new
                    {
                        @object = "block",
                        type = "paragraph",
                        paragraph = new
                        {
                            rich_text = new[]
                            {
                                new { type = "text", text = new { content = request.Contenido } }
                            }
                        }
                    }
                }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("https://api.notion.com/v1/pages", content);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, $"Error de Notion al crear página: {body}");

        using var doc = JsonDocument.Parse(body);
        return Ok(new
        {
            message = "Nota creada en Notion",
            id = doc.RootElement.GetProperty("id").GetString(),
            url = doc.RootElement.GetProperty("url").GetString(),
            titulo = request.Titulo
        });
    }

    [HttpGet]
    public async Task<IActionResult> ListarPaginas()
    {
        var token = HttpContext.Session.GetString("NotionAccessToken")!;
        var client = CrearClienteNotion(token);

        var searchBody = new StringContent(
            JsonSerializer.Serialize(new { page_size = 15 }),
            Encoding.UTF8,
            "application/json"
        );

        var response = await client.PostAsync("https://api.notion.com/v1/search", searchBody);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, $"Error de Notion al listar páginas: {body}");

        using var doc = JsonDocument.Parse(body);
        var items = new List<object>();

        foreach (var item in doc.RootElement.GetProperty("results").EnumerateArray())
        {
            var id = item.GetProperty("id").GetString();
            var objType = item.GetProperty("object").GetString();
            var titulo = ExtraerTitulo(item);
            items.Add(new { id, tipo = objType, titulo });
        }

        return Ok(new { total = items.Count, items });
    }

    private HttpClient CrearClienteNotion(string accessToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Add("Notion-Version", "2022-06-28");
        return client;
    }

    private static string ExtraerTitulo(JsonElement item)
    {
        if (item.TryGetProperty("properties", out var props))
        {
            foreach (var prop in props.EnumerateObject())
            {
                if (prop.Value.TryGetProperty("title", out var titleArr) &&
                    titleArr.ValueKind == JsonValueKind.Array &&
                    titleArr.GetArrayLength() > 0)
                {
                    if (titleArr[0].TryGetProperty("plain_text", out var pt))
                        return pt.GetString() ?? "(Sin título)";
                }
            }
        }
        return "(Sin título)";
    }
    }
}
