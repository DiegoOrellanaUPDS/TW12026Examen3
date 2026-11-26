namespace Examen3.Epic.Models;

public sealed record Note(
    Guid Id,
    string Text,
    DateTimeOffset CreatedAt,
    string AuthorName,
    string Provider);

public sealed class CreateNoteRequest
{
    public string? Text { get; set; }
}
