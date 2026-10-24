using Google.Apis.Auth;

namespace CRUDAppUsingADO.Services;

public interface IGoogleAuthService
{
    Task<GoogleJsonWebSignature.Payload?> VerifyGoogleTokenAsync(string idToken);
}