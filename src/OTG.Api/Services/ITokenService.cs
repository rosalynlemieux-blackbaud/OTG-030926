using OTG.Domain.Identity;

namespace OTG.Api.Services;

public interface ITokenService
{
    string CreateAccessToken(User user);
}
