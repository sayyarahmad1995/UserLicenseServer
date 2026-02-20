using Core.DTOs;
using Core.Helpers;
using Core.Spec;

namespace Core.Interfaces;

public interface IUserCacheService
{
    Task<Pagination<UserDto>?> GetUsersAsync(UserSpecParams specParams);
    Task CacheUsersAsync(UserSpecParams specParams, Pagination<UserDto> data);
    Task InvalidateUserAsync(int id);
    Task InvalidateUsersAsync();
    Task<UserDto?> GetUserAsync(int id);
    Task CacheUserAsync(int id, UserDto user);
}