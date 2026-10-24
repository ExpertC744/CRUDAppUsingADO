namespace AuthSystem.API.Models.DTOs;

public class GoogleLoginDto
{
    public string IdToken { get; set; } = string.Empty;  // Google ID token from frontend
}