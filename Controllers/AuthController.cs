using AuthSystem.API.Models.DTOs;
//using AuthSystem.API.Services;
using CRUDAppUsingADO.DataAccess;
using CRUDAppUsingADO.Services;
using CRUDAppUsingADO.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CRUDAppUsingADO.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserDAL _userDal;
        private readonly TokenDAL _tokenDal;
        private readonly IJwtService _jwtService;
        private readonly IPasswordService _passwordService;
        private readonly IGoogleAuthService _googleAuthService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthController> _log;

        public AuthController(
            UserDAL userDal, TokenDAL tokenDal, IJwtService jwtService,
            IPasswordService passwordService, IGoogleAuthService googleAuthService,
            IEmailService emailService, IConfiguration config, ILogger<AuthController> log)
        {
            _userDal = userDal;
            _tokenDal = tokenDal;
            _jwtService = jwtService;
            _passwordService = passwordService;
            _googleAuthService = googleAuthService;
            _emailService = emailService;
            _config = config;
            _log = log;
        }

        // ════════════════════════════════════════════════════════
        // POST /api/auth/register
        // ════════════════════════════════════════════════════════
        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new AuthResponseDto { Success = false, Message = "Invalid input data." });

            var (hash, salt) = _passwordService.HashPassword(dto.Password);

            var (resultCode, message, userId) = await _userDal.RegisterUserAsync(
                dto.FullName, dto.Email, dto.PhoneNumber, hash, salt, roleName: "User");

            if (resultCode != 1)
            {
                await _userDal.InsertAuditLogAsync(null, dto.Email, "Register", GetIp(), GetUserAgent(), false, message);
                return BadRequest(new AuthResponseDto { Success = false, Message = message });
            }

            await _userDal.InsertAuditLogAsync(userId, dto.Email, "Register", GetIp(), GetUserAgent(), true);

            // Fire-and-forget welcome email (don't block the response on SMTP)
            _ = _emailService.SendWelcomeEmailAsync(dto.Email, dto.FullName);

            // Auto-login after registration: issue tokens immediately
            var user = await _userDal.GetUserByEmailAsync(dto.Email);
            var tokens = await IssueTokensAsync(user!);

            return Ok(tokens);
        }

        // ── Shared helper used by Register, Login, RefreshToken, GoogleLogin ──
        private async Task<AuthResponseDto> IssueTokensAsync(Models.User user)
        {
            var (accessToken, jwtId, expiresAt) = _jwtService.GenerateAccessToken(user);
            var refreshToken = _jwtService.GenerateRefreshToken();

            var refreshExpiryDays = int.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7");
            await _tokenDal.SaveRefreshTokenAsync(
                user.UserId, refreshToken, jwtId,
                DateTime.UtcNow.AddDays(refreshExpiryDays), GetIp());

            return new AuthResponseDto
            {
                Success = true,
                Message = "Success.",
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = expiresAt,
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.RoleName,
                ProfilePicUrl = user.ProfilePicUrl
            };
        }

        private string? GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

        private string? GetUserAgent() => Request.Headers["User-Agent"].ToString();

        // ════════════════════════════════════════════════════════
        // POST /api/auth/login
        // ════════════════════════════════════════════════════════
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new AuthResponseDto { Success = false, Message = "Invalid input." });

            var user = await _userDal.GetUserByEmailAsync(dto.Email);

            if (user == null)
            {
                await _userDal.InsertAuditLogAsync(null, dto.Email, "FailedLogin", GetIp(), GetUserAgent(), false, "User not found");
                return Unauthorized(new AuthResponseDto { Success = false, Message = "Invalid email or password." });
            }

            // Account lockout check (5 failed attempts → 15 min lock, set by sp_UpdateFailedLoginCount)
            if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.UtcNow)
            {
                var minutesLeft = Math.Ceiling((user.LockoutUntil.Value - DateTime.UtcNow).TotalMinutes);
                return Unauthorized(new AuthResponseDto
                {
                    Success = false,
                    Message = $"Account locked due to multiple failed attempts. Try again in {minutesLeft} minute(s)."
                });
            }

            if (!user.IsActive)
                return Unauthorized(new AuthResponseDto { Success = false, Message = "This account has been deactivated." });

            // Google-only accounts have no password set
            if (string.IsNullOrEmpty(user.PasswordHash))
                return Unauthorized(new AuthResponseDto
                {
                    Success = false,
                    Message = "This account uses Google Sign-In. Please log in with Google."
                });

            var passwordValid = _passwordService.VerifyPassword(dto.Password, user.PasswordHash, user.PasswordSalt!);

            if (!passwordValid)
            {
                await _userDal.UpdateFailedLoginCountAsync(dto.Email, reset: false);
                await _userDal.InsertAuditLogAsync(user.UserId, dto.Email, "FailedLogin", GetIp(), GetUserAgent(), false, "Wrong password");
                return Unauthorized(new AuthResponseDto { Success = false, Message = "Invalid email or password." });
            }

            // Success: reset failed counter, log, issue tokens
            await _userDal.UpdateFailedLoginCountAsync(dto.Email, reset: true);
            await _userDal.InsertAuditLogAsync(user.UserId, dto.Email, "Login", GetIp(), GetUserAgent(), true);

            var tokens = await IssueTokensAsync(user);

            // Set refresh token as HttpOnly cookie (defense-in-depth alongside returning it in body)
            SetRefreshTokenCookie(tokens.RefreshToken!);

            return Ok(tokens);
        }

        private void SetRefreshTokenCookie(string token)
        {
            Response.Cookies.Append("refreshToken", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(int.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7"))
            });
        }


        // ════════════════════════════════════════════════════════
        // POST /api/auth/refresh-token
        // ════════════════════════════════════════════════════════
        [HttpPost("refresh-token")]
        public async Task<ActionResult<AuthResponseDto>> RefreshToken([FromBody] RefreshTokenRequestDto dto)
        {
            // Allow refresh token to come from cookie if not in body
            var refreshToken = !string.IsNullOrEmpty(dto.RefreshToken)
                ? dto.RefreshToken
                : Request.Cookies["refreshToken"];

            if (string.IsNullOrEmpty(refreshToken))
                return Unauthorized(new AuthResponseDto { Success = false, Message = "No refresh token provided." });

            ClaimsPrincipal principal;
            try
            {
                principal = _jwtService.GetPrincipalFromExpiredToken(dto.AccessToken);
            }
            catch
            {
                return Unauthorized(new AuthResponseDto { Success = false, Message = "Invalid access token." });
            }

            var jwtId = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
            var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (jwtId == null || userIdClaim == null)
                return Unauthorized(new AuthResponseDto { Success = false, Message = "Invalid token claims." });

            var storedToken = await _tokenDal.GetRefreshTokenAsync(refreshToken);

            if (storedToken == null)
                return Unauthorized(new AuthResponseDto { Success = false, Message = "Refresh token not found." });

            if (storedToken.IsRevoked)
            {
                // 🚨 SECURITY: a revoked token being reused means it was likely stolen.
                // Revoke the entire token family for this user as a precaution.
                await _tokenDal.RevokeAllUserTokensAsync(storedToken.UserId);
                await _userDal.InsertAuditLogAsync(storedToken.UserId, null, "TokenReuse", GetIp(), GetUserAgent(), false,
                    "Revoked refresh token reused — possible theft. All sessions revoked.");
                return Unauthorized(new AuthResponseDto
                {
                    Success = false,
                    Message = "Security alert: this session has been invalidated. Please log in again."
                });
            }

            if (storedToken.ExpiresAt < DateTime.UtcNow)
                return Unauthorized(new AuthResponseDto { Success = false, Message = "Refresh token expired. Please log in again." });

            if (storedToken.JwtId != jwtId)
                return Unauthorized(new AuthResponseDto { Success = false, Message = "Token mismatch." });

            var user = await _userDal.GetUserByIdAsync(Guid.Parse(userIdClaim));
            if (user == null || !user.IsActive)
                return Unauthorized(new AuthResponseDto { Success = false, Message = "User not found or inactive." });

            // Re-fetch with password fields populated (GetUserByIdAsync excludes them) — not needed here
            // since we already have RoleName etc. for token generation.

            // Issue new token pair, rotate (revoke old, issue new)
            var (newAccessToken, newJwtId, newExpiresAt) = _jwtService.GenerateAccessToken(user);
            var newRefreshToken = _jwtService.GenerateRefreshToken();

            await _tokenDal.RevokeRefreshTokenAsync(refreshToken, GetIp(), newRefreshToken);

            var refreshExpiryDays = int.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7");
            await _tokenDal.SaveRefreshTokenAsync(
                user.UserId, newRefreshToken, newJwtId,
                DateTime.UtcNow.AddDays(refreshExpiryDays), GetIp());

            await _userDal.InsertAuditLogAsync(user.UserId, user.Email, "TokenRefresh", GetIp(), GetUserAgent(), true);

            SetRefreshTokenCookie(newRefreshToken);

            return Ok(new AuthResponseDto
            {
                Success = true,
                Message = "Token refreshed.",
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                AccessTokenExpiresAt = newExpiresAt,
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.RoleName,
                ProfilePicUrl = user.ProfilePicUrl
            });
        }

        // ════════════════════════════════════════════════════════
        // POST /api/auth/logout
        // Revokes only the current session's refresh token.
        // ════════════════════════════════════════════════════════
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto? dto)
        {
            var refreshToken = dto?.RefreshToken ?? Request.Cookies["refreshToken"];

            if (!string.IsNullOrEmpty(refreshToken))
            {
                await _tokenDal.RevokeRefreshTokenAsync(refreshToken, GetIp());
            }

            Response.Cookies.Delete("refreshToken");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _userDal.InsertAuditLogAsync(
                userId != null ? Guid.Parse(userId) : null, null, "Logout", GetIp(), GetUserAgent(), true);

            return Ok(new { Message = "Logged out successfully." });
        }

        // ════════════════════════════════════════════════════════
        // POST /api/auth/logout-all
        // Revokes ALL sessions for the user (e.g. "log out everywhere" button)
        // ════════════════════════════════════════════════════════
        [HttpPost("logout-all")]
        [Authorize]
        public async Task<IActionResult> LogoutAll()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _tokenDal.RevokeAllUserTokensAsync(userId);
            Response.Cookies.Delete("refreshToken");
            await _userDal.InsertAuditLogAsync(userId, null, "LogoutAll", GetIp(), GetUserAgent(), true);
            return Ok(new { Message = "Logged out of all devices." });
        }


        // ════════════════════════════════════════════════════════
        // POST /api/auth/google-login
        // Frontend gets an ID token from Google Sign-In button,
        // sends it here for server-side verification.
        // ════════════════════════════════════════════════════════
        [HttpPost("google-login")]
        public async Task<ActionResult<AuthResponseDto>> GoogleLogin([FromBody] GoogleLoginDto dto)
        {
            var payload = await _googleAuthService.VerifyGoogleTokenAsync(dto.IdToken);

            if (payload == null)
                return Unauthorized(new AuthResponseDto { Success = false, Message = "Invalid Google token." });

            var (isNewUser, userId) = await _userDal.GetOrCreateGoogleUserAsync(
                payload.Subject, payload.Email, payload.Name, payload.Picture);

            var user = await _userDal.GetUserByIdAsync(userId);
            if (user == null)
                return StatusCode(500, new AuthResponseDto { Success = false, Message = "Failed to load user." });

            if (!user.IsActive)
                return Unauthorized(new AuthResponseDto { Success = false, Message = "Account deactivated." });

            if (isNewUser)
                _ = _emailService.SendWelcomeEmailAsync(user.Email, user.FullName);

            await _userDal.InsertAuditLogAsync(user.UserId, user.Email, "GoogleLogin", GetIp(), GetUserAgent(), true);

            var tokens = await IssueTokensAsync(user);
            SetRefreshTokenCookie(tokens.RefreshToken!);

            return Ok(tokens);
        }

        // ════════════════════════════════════════════════════════
        // POST /api/auth/forgot-password
        // ════════════════════════════════════════════════════════
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var user = await _userDal.GetUserByEmailAsync(dto.Email);

            // Always return the same message — don't leak whether an email exists
            const string genericMessage = "If that email exists in our system, a reset link has been sent.";

            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
                return Ok(new { Message = genericMessage });

            var resetToken = _passwordService.GenerateSecureToken();
            await _userDal.SavePasswordResetTokenAsync(user.UserId, resetToken, DateTime.UtcNow.AddMinutes(30));

            var resetLink = $"{_config["Frontend:Url"]}{_config["Frontend:ResetPasswordPath"]}?token={Uri.EscapeDataString(resetToken)}";
            _ = _emailService.SendPasswordResetEmailAsync(user.Email, user.FullName, resetLink);

            await _userDal.InsertAuditLogAsync(user.UserId, dto.Email, "ForgotPasswordRequest", GetIp(), GetUserAgent(), true);

            return Ok(new { Message = genericMessage });
        }

        // ════════════════════════════════════════════════════════
        // POST /api/auth/reset-password
        // ════════════════════════════════════════════════════════
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { Message = "Invalid input." });

            var (userId, expiresAt, isUsed) = await _userDal.ValidatePasswordResetTokenAsync(dto.Token);

            if (userId == null)
                return BadRequest(new { Message = "Invalid reset token." });

            if (isUsed)
                return BadRequest(new { Message = "This reset link has already been used." });

            if (expiresAt < DateTime.UtcNow)
                return BadRequest(new { Message = "This reset link has expired. Please request a new one." });

            var (hash, salt) = _passwordService.HashPassword(dto.NewPassword);
            await _userDal.ResetPasswordAsync(userId.Value, hash, salt, dto.Token);

            await _userDal.InsertAuditLogAsync(userId, null, "PasswordReset", GetIp(), GetUserAgent(), true);

            return Ok(new { Message = "Password reset successfully. Please log in with your new password." });
        }

        // ════════════════════════════════════════════════════════
        // GET /api/auth/me — current authenticated user's profile
        // ════════════════════════════════════════════════════════
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _userDal.GetUserByIdAsync(userId);
            if (user == null) return NotFound();

            return Ok(new
            {
                user.UserId,
                user.FullName,
                user.Email,
                user.PhoneNumber,
                user.RoleName,
                user.IsEmailVerified,
                user.ProfilePicUrl,
                user.CreatedAt
            });
        }
    }
}
