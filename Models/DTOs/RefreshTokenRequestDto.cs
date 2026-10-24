namespace AuthSystem.API.Models.DTOs;

public class RefreshTokenRequestDto
{
    public string AccessToken { get; set; } = string.Empty;   // expired JWT
    public string RefreshToken { get; set; } = string.Empty;
}