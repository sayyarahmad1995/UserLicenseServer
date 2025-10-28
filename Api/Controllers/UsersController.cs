using Api.DTOs;
using Api.Helpers;
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
    public async Task<ActionResult<Pagination<UserDto>>> GetUsers([FromQuery] UserSpecParams specParams)
    {
        var spec = new UserSpecification(specParams);
        var countSpec = new UserCountSpecification(specParams);

        var totalItems = await _unitOfWork.Repository<User>().CountAsync(countSpec);
        var users = await _unitOfWork.Repository<User>().ListAsync(spec);

        var mappedData = _mapper.Map<IReadOnlyList<UserDto>>(users);
        var data = new Pagination<UserDto>
        {
            PageIndex = specParams.PageIndex,
            PageSize = specParams.PageSize,
            TotalCount = totalItems,
            Data = mappedData
        };
        return Ok(data);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUserById(int id)
    {
        var spec = new UserSpecification(id);
        var user = await _unitOfWork.Repository<User>().GetEntityWithSpec(spec);

        if (user == null)
            return NotFound(new { message = "User not found" });

        var data = _mapper.Map<UserWithLicensesDto>(user);

        return Ok(data);
    }
}
