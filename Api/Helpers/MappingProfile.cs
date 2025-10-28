using Api.DTOs;
using AutoMapper;
using Core.Entities;

namespace Api.Helpers;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<User, UserDto>();

        CreateMap<User, UserWithLicensesDto>()
        .IncludeBase<User, UserDto>()
        .ForMember(dest => dest.Licenses, opt => opt.MapFrom(src => src.Licenses));

        CreateMap<License, LicenseDto>();
    }
}
