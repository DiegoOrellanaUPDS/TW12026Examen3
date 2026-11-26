using System.Security.Claims;
using Examen3.Epic.Auth;
using Examen3.Epic.Models;
using Examen3.Epic.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Examen3.Epic.Controllers;

[ApiController]
[Route("api/notes")]
[Authorize(Policy = EpicConstants.EpicPolicy)]
public sealed class NotesController : ControllerBase
{
    private const int MaxNoteLength = 500;

    private readonly INoteStore _notes;

    public NotesController(INoteStore notes)
    {
        _notes = notes;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var identity = GetIdentity();

        if (identity is null)
        {
            return UnauthorizedProblem();
        }

        var notes = await _notes.GetForUserAsync(identity.Value.UserId, cancellationToken);

        return Ok(new
        {
            count = notes.Count,
            user = new
            {
                id = identity.Value.UserId,
                username = identity.Value.DisplayName,
                provider = EpicConstants.ProviderDisplayName
            },
            notes
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateNoteRequest? request,
        CancellationToken cancellationToken)
    {
        var identity = GetIdentity();

        if (identity is null)
        {
            return UnauthorizedProblem();
        }

        var text = request?.Text?.Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest(new
            {
                status = StatusCodes.Status400BadRequest,
                title = "Texto requerido",
                detail = "La nota no puede estar vacía."
            });
        }

        if (text.Length > MaxNoteLength)
        {
            return BadRequest(new
            {
                status = StatusCodes.Status400BadRequest,
                title = "Texto demasiado largo",
                detail = $"La nota admite como máximo {MaxNoteLength} caracteres."
            });
        }

        var note = await _notes.AddAsync(
            identity.Value.UserId,
            text,
            identity.Value.DisplayName,
            EpicConstants.ProviderDisplayName,
            cancellationToken);

        return Created("/api/notes", new { note });
    }

    private (string UserId, string DisplayName)? GetIdentity()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var displayName = User.FindFirstValue(ClaimTypes.Name);

        return string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(displayName)
            ? null
            : (userId, displayName);
    }

    private ObjectResult UnauthorizedProblem() =>
        Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "No autenticado",
            detail: "Inicia sesión con Epic Games para acceder a las notas.");
}
