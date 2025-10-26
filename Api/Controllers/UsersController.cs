using Api.DTOs;
using AutoMapper;
using Core.Entities;
using Core.Interfaces;
using Core.Spec;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

public class UsersController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    public UsersController(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<IActionResult> Users(
        [FromQuery] string? username = null,
        [FromQuery] string? email = null,
        [FromQuery] string? role = null,
        [FromQuery] DateTime? createdAfter = null,
        [FromQuery] DateTime? createdBefore = null,
        [FromQuery] bool? isVerified = null
    )
    {
        var spec = new UserSpecification(username, email, role, createdAfter, createdBefore, isVerified);
        var users = await _unitOfWork.Repository<User>().ListAsync(spec);

        var userDtos = _mapper.Map<IReadOnlyList<UserDto>>(users);
        return Ok(userDtos);
    }
}
