using Core.Helpers;

namespace Api.Extensions;

/// <summary>
/// Extension methods for adding standard pagination metadata headers to HTTP responses.
/// </summary>
public static class PaginationHeaderExtensions
{
    /// <summary>
    /// Adds X-Total-Count, X-Page-Index, X-Page-Size, and X-Total-Pages headers
    /// to the response so clients can implement pagination controls without parsing the body.
    /// Also exposes these headers via Access-Control-Expose-Headers for CORS clients.
    /// </summary>
    public static void AddPaginationHeaders<T>(this HttpResponse response, Pagination<T> pagination)
    {
        response.Headers["X-Total-Count"] = pagination.TotalCount.ToString();
        response.Headers["X-Page-Index"] = pagination.PageIndex.ToString();
        response.Headers["X-Page-Size"] = pagination.PageSize.ToString();
        response.Headers["X-Total-Pages"] = pagination.TotalPages.ToString();
        response.Headers["Access-Control-Expose-Headers"] =
            "X-Total-Count, X-Page-Index, X-Page-Size, X-Total-Pages";
    }
}
