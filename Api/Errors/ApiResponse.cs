namespace Api.Errors;

public class ApiResponse
{
   public int StatusCode { get; set; }
   public string? Message { get; set; }
   public object? Data { get; set; }

   public ApiResponse(int statusCode, string? message = null)
   {
      StatusCode = statusCode;
      Message = message ?? GetDefaultMessageForStatusCode(statusCode);
   }

   public ApiResponse(int statusCode, object data, string? message = null)
   {
      StatusCode = statusCode;
      Message = message ?? GetDefaultMessageForStatusCode(statusCode);
      Data = data;
   }

   private static string? GetDefaultMessageForStatusCode(int statusCode)
   {
      return statusCode switch
      {
         400 => "You have made a bad request",
         401 => "You are not authorized",
         403 => "Access is forbidden",
         404 => "Resource not found",
         500 => "An internal server error occurred",
         _ => null
      };
   }
}
