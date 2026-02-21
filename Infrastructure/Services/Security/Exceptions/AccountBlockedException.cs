namespace Infrastructure.Services.Exceptions;

/// <summary>
/// Exception thrown when a blocked user attempts to log in.
/// </summary>
public class AccountBlockedException : Exception
{
    public AccountBlockedException() : base("Your account has been blocked. Please contact support.") { }
    public AccountBlockedException(string message) : base(message) { }
}
