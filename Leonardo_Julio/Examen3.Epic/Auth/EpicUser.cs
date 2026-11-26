namespace Examen3.Epic.Auth;

public sealed record EpicUser(
    string AccountId,
    string DisplayName,
    string? PreferredUsername,
    string? AvatarUrl);
