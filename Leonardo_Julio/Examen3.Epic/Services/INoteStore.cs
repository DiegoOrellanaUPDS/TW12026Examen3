using Examen3.Epic.Models;

namespace Examen3.Epic.Services;

public interface INoteStore
{
    Task<IReadOnlyList<Note>> GetForUserAsync(string userId, CancellationToken cancellationToken);

    Task<Note> AddAsync(
        string userId,
        string text,
        string authorName,
        string provider,
        CancellationToken cancellationToken);
}
