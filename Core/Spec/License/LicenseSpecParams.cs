namespace Core.Spec;

public class LicenseSpecParams
{
    private const int MaxPageSize = 50;
    private int _pageIndex = 1;
    public int PageIndex
    {
        get => _pageIndex;
        set => _pageIndex = value < 1 ? 1 : value;
    }
    private int _pageSize = 10;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 1 : value > MaxPageSize ? MaxPageSize : value;
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
