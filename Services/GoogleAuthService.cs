using Google.Apis.Auth;

namespace CRUDAppUsingADO.Services;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly IConfiguration _config;
    private readonly ILogger<GoogleAuthService> _log;

    public GoogleAuthService(IConfiguration config, ILogger<GoogleAuthService> log)
    {
        _config = config;
        _log = log;
    }

    public async Task<GoogleJsonWebSignature.Payload?> VerifyGoogleTokenAsync(string idToken)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _config["Google:ClientId"] }
            };
            return await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
        }
        catch (InvalidJwtException ex)
        {
            _log.LogWarning("Invalid Google token: {Message}", ex.Message);
            return null;
        }
    }
}