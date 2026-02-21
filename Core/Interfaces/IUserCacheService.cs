using Core.DTOs;
using Core.Helpers;
using Core.Spec;

namespace Core.Interfaces;

public interface IUserCacheService
{
    Task<Pagination<UserDto>?> GetUsersAsync(UserSpecParams specParams, CancellationToken ct = default);
    Task CacheUsersAsync(UserSpecParams specParams, Pagination<UserDto> data, CancellationToken ct = default);
    Task InvalidateUserAsync(int id);
    Task InvalidateUsersAsync();
    Task<UserDto?> GetUserAsync(int id, CancellationToken ct = default);
    Task CacheUserAsync(int id, UserDto user, CancellationToken ct = default);
}