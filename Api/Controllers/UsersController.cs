using Api.DTOs;
using AutoMapper;
using Core.Entities;
using Core.Interfaces;
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
    public async Task<IActionResult> Users()
    {
        var userRepo = _unitOfWork.Repository<User>();
        var users = await userRepo.ListAllAsync();
        var userDtos = _mapper.Map<IReadOnlyList<UserDto>>(users);
        return Ok(userDtos);
    }
}
