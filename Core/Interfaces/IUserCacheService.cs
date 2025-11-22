using Core.DTOs;
using Core.Helpers;
using Core.Spec;

namespace Core.Interfaces;

public interface IUserCacheService
{
    Task<Pagination<UserDto>?> GetUsersAsync(UserSpecParams specParams);
    Task CacheUsersAsync(UserSpecParams specParams, Pagination<UserDto> data);
    Task InvalidateUsersAsync();
}