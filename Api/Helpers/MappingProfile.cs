using Core.DTOs;
using AutoMapper;
using Core.Entities;

namespace Api.Helpers;

public class MappingProfile : Profile
{
   public MappingProfile()
   {
      CreateMap<User, UserDto>();

      CreateMap<License, LicenseDto>();
   }
}
