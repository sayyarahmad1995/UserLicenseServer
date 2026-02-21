using AutoMapper;
using Core.DTOs;
using Core.Entities;
using Core.Enums;

namespace Api.Helpers;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<User, UserDto>();

        CreateMap<License, LicenseDto>()
            .ForMember(dest => dest.ActiveActivations,
                opt => opt.MapFrom(src => src.Activations.Count(a => a.DeactivatedAt == null)));

        CreateMap<LicenseActivation, LicenseActivationDto>()
            .ForMember(dest => dest.IsActive,
                opt => opt.MapFrom(src => src.DeactivatedAt == null));

        CreateMap<RegisterDto, User>()
           .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
           .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
           .ForMember(dest => dest.Status, opt => opt.MapFrom(_ => UserStatus.Unverified));

        CreateMap<AuditLog, AuditLogDto>();
    }
}
