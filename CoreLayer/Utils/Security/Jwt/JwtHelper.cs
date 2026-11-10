using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using CoreLayer.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CoreLayer.Utils.Security.Jwt;

public class JwtHelper(IConfiguration configuration) : IJwtTokenHelper
{
    public AccessToken CreateAccessToken(
        Account account,
        List<OperationClaim> operationClaims)
    {
        var tokenOptions = configuration
            .GetSection("TokenOptions")
            .Get<TokenOptions>()
            ?? throw new InvalidOperationException("TokenOptions configuration is missing.");

        var privateKeyPath = Path.GetFullPath(tokenOptions.PrivateKeyPath);

        if (!File.Exists(privateKeyPath))
            throw new FileNotFoundException("RSA private key was not found.", privateKeyPath);

        using var rsa = RSA.Create();

        rsa.ImportFromPem(File.ReadAllText(privateKeyPath));

        var securityKey = new RsaSecurityKey(rsa);

        var signingCredentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.RsaSha256
        );

        var jwtToken = CreateJwtSecurityToken(
            tokenOptions,
            account,
            signingCredentials,
            operationClaims
        );

        var tokenHandler = new JwtSecurityTokenHandler();

        var token = tokenHandler.WriteToken(jwtToken);

        return new AccessToken
        {
            Token = token,
            Expiration = jwtToken.ValidTo
        };
    }

    public JwtSecurityToken CreateJwtSecurityToken(
        TokenOptions tokenOptions,
        Account account,
        SigningCredentials signingCredentials,
        List<OperationClaim> operationClaims)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, account.Id.ToString())
        };

        claims.AddRange(operationClaims.Select(operationClaim =>
            new Claim(ClaimTypes.Role, operationClaim.Name)
        ));

        var now = DateTime.UtcNow;

        return new JwtSecurityToken(
            issuer: tokenOptions.Issuer,
            audience: tokenOptions.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(tokenOptions.AccessTokenExpiration),
            signingCredentials: signingCredentials
        );
    }
}