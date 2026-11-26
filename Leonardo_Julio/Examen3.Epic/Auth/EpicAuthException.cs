namespace Examen3.Epic.Auth;

public sealed class EpicAuthException : Exception
{
    public EpicAuthException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}
