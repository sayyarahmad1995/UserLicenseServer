namespace Core.Entities;

/// <summary>
/// Base class for all domain entities, providing a primary key.
/// </summary>
public class BaseEntity
{
    /// <summary>Primary key identifier.</summary>
    public int Id { get; set; }
}
