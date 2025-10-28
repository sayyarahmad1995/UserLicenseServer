using System.Text.Json.Serialization;

namespace Api.DTOs;

public class UserWithLicensesDto : UserDto
{
    [JsonPropertyOrder(100)]
    public IReadOnlyList<LicenseDto>? Licenses { get; set; }
}
