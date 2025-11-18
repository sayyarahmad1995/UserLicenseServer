using Core.DTOs;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Interfaces;

public interface IAuthService
{
    Task<LoginResultDto> LoginAsync(LoginDto dto, HttpResponse response);
}
