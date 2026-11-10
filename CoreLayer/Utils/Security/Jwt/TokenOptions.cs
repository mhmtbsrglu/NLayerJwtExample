namespace CoreLayer.Utils.Security.Jwt;

public class TokenOptions
{
    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public int AccessTokenExpiration { get; set; }

    public string PrivateKeyPath { get; set; } = string.Empty;

    public string PublicKeyPath { get; set; } = string.Empty;
}