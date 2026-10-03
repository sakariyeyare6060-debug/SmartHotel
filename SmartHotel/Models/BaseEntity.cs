namespace SmartHotel.Models;

public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Entities with a human-readable document number (BK-0001, INV-0001 ...).
/// The number is derived from the identity value, so it is assigned by
/// <see cref="Data.AppDbContext"/> right after the row is first inserted.
/// </summary>
public interface INumbered
{
    bool NeedsNumber { get; }
    void AssignNumber();
}

public static class DocumentNumber
{
    public const string TempPrefix = "TMP-";

    public static string Temp() => TempPrefix + Guid.NewGuid().ToString("N")[..14];
}
