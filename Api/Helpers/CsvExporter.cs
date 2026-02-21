using System.Reflection;
using System.Text;

namespace Api.Helpers;

/// <summary>
/// Utility for exporting collections to CSV format.
/// Uses reflection to read public property names as headers and values as rows.
/// </summary>
public static class CsvExporter
{
    /// <summary>
    /// Converts a collection of DTOs to a UTF-8 CSV byte array.
    /// </summary>
    public static byte[] ToCsv<T>(IEnumerable<T> items)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var sb = new StringBuilder();

        // Header row
        sb.AppendLine(string.Join(",", properties.Select(p => EscapeCsv(p.Name))));

        // Data rows
        foreach (var item in items)
        {
            var values = properties.Select(p =>
            {
                var value = p.GetValue(item);
                return value switch
                {
                    null => "",
                    DateTime dt => dt.ToString("o"),
                    _ => value.ToString() ?? ""
                };
            });
            sb.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
