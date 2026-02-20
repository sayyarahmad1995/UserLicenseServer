namespace Infrastructure.Services.Exceptions;

/// <summary>
/// Exception thrown when user credentials are invalid during authentication.
/// </summary>
public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("Invalid username or password") { }
    public InvalidCredentialsException(string message) : base(message) { }
}
