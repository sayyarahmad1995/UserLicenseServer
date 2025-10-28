namespace Core.Spec;

public class UserSpecParams
{
    private const int MaxPageSize = 50;
    public int PageIndex { get; set; } = 1;
    private int _pageSize = 10;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = (value > MaxPageSize) ? MaxPageSize : value;
    }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public DateTime? _CreatedAfter { get; set; }
    public DateTime? CreatedAfter
    {
        get => _CreatedAfter;
        set => _CreatedAfter = value.HasValue
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : null;
    }
    public DateTime? _CreatedBefore { get; set; }
    public DateTime? CreatedBefore
    {
        get => _CreatedBefore;
        set => _CreatedBefore = value.HasValue
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : null;
    }
    public bool? IsVerified { get; set; }
    public bool IncludeLicenses { get; set; }
    public string? Sort { get; set; }
    private string? _search;
    public string? Search
    {
        get => _search;
        set => _search = value?.ToLower();
    }
}
