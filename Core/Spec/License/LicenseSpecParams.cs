namespace Core.Spec;

public class LicenseSpecParams
{
    private const int MaxPageSize = 50;
    public int PageIndex { get; set; } = 1;
    private int _pageSize { get; set; } = 10;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = (value > MaxPageSize) ? MaxPageSize : value;
    }
    public int? UserId { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public DateTime? ExpiredAfter { get; set; }
    public DateTime? ExpiredBefore { get; set; }
    public string? Status { get; set; }
    public string? Sort { get; set; }
    private string? _search;
    public string? Search
    {
        get => _search;
        set => _search = value;
    }
}
