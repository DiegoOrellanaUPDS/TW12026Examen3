using System.Text.Json;
using Examen3.Epic.Models;
using Examen3.Epic.Options;
using Microsoft.Extensions.Options;

namespace Examen3.Epic.Services;

/// <summary>
/// Persistencia sencilla en un archivo JSON, separada por identificador de
/// usuario. El archivo vive en App_Data y no se versiona.
/// </summary>
public sealed class JsonNoteStore : INoteStore
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

    private readonly string _filePath;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<JsonNoteStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonNoteStore(
        IOptions<StorageOptions> options,
        IWebHostEnvironment environment,
        TimeProvider timeProvider,
        ILogger<JsonNoteStore> logger)
    {
        var configuredPath = options.Value.NotesFile;

        _filePath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);

        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Note>> GetForUserAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            var data = await LoadAsync(cancellationToken);

            return data.TryGetValue(userId, out var notes)
                ? notes.OrderByDescending(note => note.CreatedAt).ToArray()
                : Array.Empty<Note>();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Note> AddAsync(
        string userId,
        string text,
        string authorName,
        string provider,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            var data = await LoadAsync(cancellationToken);

            if (!data.TryGetValue(userId, out var notes))
            {
                notes = new List<Note>();
                data[userId] = notes;
            }

            var note = new Note(
                Guid.NewGuid(),
                text,
                _timeProvider.GetUtcNow(),
                authorName,
                provider);

            notes.Add(note);
            await SaveAsync(data, cancellationToken);

            return note;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, List<Note>>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, List<Note>>(StringComparer.Ordinal);
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var data = await JsonSerializer.DeserializeAsync<Dictionary<string, List<Note>>>(
                stream,
                SerializerOptions,
                cancellationToken);

            return data is null
                ? new Dictionary<string, List<Note>>(StringComparer.Ordinal)
                : new Dictionary<string, List<Note>>(data, StringComparer.Ordinal);
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "El archivo de notas {FilePath} no contiene JSON válido.", _filePath);
            throw new InvalidOperationException(
                "El archivo de notas está dañado. Revisa App_Data/notes.json.",
                exception);
        }
    }

    private async Task SaveAsync(
        Dictionary<string, List<Note>> data,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, data, SerializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
