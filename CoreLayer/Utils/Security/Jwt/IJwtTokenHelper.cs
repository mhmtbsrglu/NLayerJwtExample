using System.IdentityModel.Tokens.Jwt;
using CoreLayer.Models;
using Microsoft.IdentityModel.Tokens;

namespace CoreLayer.Utils.Security.Jwt;

public interface IJwtTokenHelper
{
    AccessToken CreateAccessToken(
        Account account,
        List<OperationClaim> operationClaims);

    JwtSecurityToken CreateJwtSecurityToken(
        TokenOptions tokenOptions,
        Account account,
        SigningCredentials signingCredentials,
        List<OperationClaim> operationClaims);
}