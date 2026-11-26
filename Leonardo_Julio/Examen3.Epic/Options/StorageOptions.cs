namespace Examen3.Epic.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string NotesFile { get; set; } = "App_Data/notes.json";
}
