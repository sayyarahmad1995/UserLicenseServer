namespace Infrastructure.Services.Exceptions;

/// <summary>
/// Exception thrown when token operations fail.
/// </summary>
public class TokenException : Exception
{
    public TokenException(string message) : base(message) { }
    public TokenException(string message, Exception innerException) : base(message, innerException) { }
}
