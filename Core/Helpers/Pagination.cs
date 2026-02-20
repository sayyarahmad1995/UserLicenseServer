namespace Core.Helpers;

public class Pagination<T>
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<T> Data { get; set; } = new List<T>();
}
