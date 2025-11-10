using System.IdentityModel.Tokens.Jwt;
using Core.DTOs;
using Core.Interfaces;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services;

public class AuthService : IAuthService
{
   private readonly IUnitOfWork _unitOfWork;
   private readonly ITokenService _tokenService;
   private readonly IAuthHelper _authHelper;
   private readonly IConfiguration _config;
   public AuthService(IUnitOfWork unitOfWork,
   ITokenService tokenService,
   IAuthHelper authHelper,
   IConfiguration config)
   {
      _config = config;
      _authHelper = authHelper;
      _tokenService = tokenService;
      _unitOfWork = unitOfWork;
   }

   public async Task<LoginResultDto> LoginAsync(LoginDto dto, HttpResponse response)
   {
      if (_authHelper.TryGetCookie(response.HttpContext.Request, "refreshToken", out var existingRefreshToken))
      {
         var isValid = await _tokenService.ValidateRefreshTokenAsync(existingRefreshToken!);
         if (isValid)
         {
            return new LoginResultDto
            {
               Message = "You are already signed in"
            };
         }
      }

      var user = await _unitOfWork.UserRepository.GetByUsernameAsync(dto.Username);
      if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
         throw new UnauthorizedAccessException("Invalid credentials");

      var accessToken = _tokenService.GenerateAccessToken(user);
      var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
      var jti = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

      var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user, jti);
      await _authHelper.SetAuthCookiesAsync(response, accessToken, refreshToken, _config);

      var accessExpiryMinutes = int.Parse(_config["Jwt:AccessTokenExpiryMinutes"]!);

      return new LoginResultDto
      {
         AccessTokenExpires = DateTime.UtcNow.AddMinutes(accessExpiryMinutes),
         Message = "Login successful"
      };
   }
}
