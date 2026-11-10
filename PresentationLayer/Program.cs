using System.Security.Cryptography;
using BusinessLogicLayer.Abstracts;
using BusinessLogicLayer.Concretes;
using BusinessLogicLayer.Dtos;
using CoreLayer.DataAccess.Abstracts;
using CoreLayer.Utils.Security.Jwt;
using DataAccessLayer.Concretes;
using Microsoft.IdentityModel.Tokens;
using PresentationLayer.Handlers;

var builder = WebApplication.CreateBuilder(args);

// Token configuration
var tokenOptions = builder.Configuration
    .GetSection("TokenOptions")
    .Get<TokenOptions>()
    ?? throw new InvalidOperationException(
        "TokenOptions configuration is missing."
    );

// Validate token configuration
if (string.IsNullOrWhiteSpace(tokenOptions.Issuer))
    throw new InvalidOperationException("Token issuer is not configured.");

if (string.IsNullOrWhiteSpace(tokenOptions.Audience))
    throw new InvalidOperationException("Token audience is not configured.");

if (tokenOptions.AccessTokenExpiration <= 0)
    throw new InvalidOperationException("Access token expiration must be greater than zero.");

if (string.IsNullOrWhiteSpace(tokenOptions.PublicKeyPath))
    throw new InvalidOperationException("RSA public key path is not configured.");

if (string.IsNullOrWhiteSpace(tokenOptions.PrivateKeyPath))
    throw new InvalidOperationException("RSA private key path is not configured.");

// Resolve public key path relative to the application content root
var publicKeyPath = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, tokenOptions.PublicKeyPath)
);

if (!File.Exists(publicKeyPath))
    throw new FileNotFoundException(
        "RSA public key was not found.",
        publicKeyPath
    );

// Resolve the private key path against the content root too, so signing works
// regardless of the process working directory. JwtHelper reads this value from
// configuration, so persist the absolute path.
var privateKeyPath = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, tokenOptions.PrivateKeyPath)
);

if (!File.Exists(privateKeyPath))
    throw new FileNotFoundException(
        "RSA private key was not found.",
        privateKeyPath
    );

builder.Configuration["TokenOptions:PrivateKeyPath"] = privateKeyPath;

// Load RSA public key
var rsa = RSA.Create();

try
{
    rsa.ImportFromPem(File.ReadAllText(publicKeyPath));
}
catch
{
    rsa.Dispose();
    throw;
}

var publicKey = new RsaSecurityKey(rsa);

// Register RSA key for application lifetime
builder.Services.AddSingleton<RsaSecurityKey>(publicKey);

// Register token options
builder.Services.Configure<TokenOptions>(
    builder.Configuration.GetSection("TokenOptions")
);

// Authentication & Authorization
builder.Services
    .AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = tokenOptions.Issuer,

            ValidateAudience = true,
            ValidAudience = tokenOptions.Audience,

            ValidateLifetime = true,
            RequireExpirationTime = true,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = publicKey,

            ValidAlgorithms =
            [
                SecurityAlgorithms.RsaSha256
            ],

            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

const string FrontendPolicy = "FrontendPolicy";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // refresh cookie için zorunlu
    });
});
// API services
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

// Dependency Injection - IoC
builder.Services.AddSingleton<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IAccountService, AccountManager>();
builder.Services.AddScoped<IJwtTokenHelper, JwtHelper>();
var app = builder.Build();

// Development endpoints
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapGet("/dev/generate-pepper", () =>
        {
            var pepper = RandomNumberGenerator.GetBytes(32);

            return Results.Ok(new
            {
                Pepper = Convert.ToBase64String(pepper),
                Length = pepper.Length,
                Algorithm = "HMAC-SHA256"
            });
        })
        .WithTags("Development")
        .WithName("GeneratePepper");
}

// HTTP pipeline
app.UseHttpsRedirection();

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();
app.UseCors(FrontendPolicy);
// Account endpoints
var accounts = app.MapGroup("/accounts")
    .WithTags("Accounts");
// POST /accounts/login
accounts.MapPost("/login", async (
        LoginDto dto,
        IAccountService service) =>
    {
        var accessToken = await service.LoginAsync(dto);

        return Results.Ok(accessToken);
    })
    .AllowAnonymous()
    .WithName("LoginAccount");

// POST /accounts/register
accounts.MapPost("/register", async (
    RegisterDto dto,
    IAccountService service) =>
{
    var account = await service.RegisterAsync(dto);
    var response = AccountResponse.FromAccount(account);

    return Results.Created(
        $"/accounts/{response.Id}",
        response
    );
})
.WithName("RegisterAccount");

// GET /accounts
accounts.MapGet("/", async (IAccountService service) =>
{
    var list = await service.ListActiveAsync();

    return Results.Ok(list.Select(AccountResponse.FromAccount));
})
.RequireAuthorization()
.WithName("ListAccounts");

// GET /accounts/{id}
accounts.MapGet("/{id:guid}", async (
    Guid id,
    IAccountService service) =>
{
    var account = await service.GetByIdAsync(id);

    return account is null
        ? Results.NotFound()
        : Results.Ok(AccountResponse.FromAccount(account));
})
.RequireAuthorization()
.WithName("GetAccountById");

// PUT /accounts/{id}/email
accounts.MapPut("/{id:guid}/email", async (
    Guid id,
    ChangeEmailRequest request,
    IAccountService service) =>
{
    await service.ChangeEmailAsync(id, request.NewEmail);

    return Results.NoContent();
})
.RequireAuthorization()
.WithName("ChangeAccountEmail");

// DELETE /accounts/{id}
accounts.MapDelete("/{id:guid}", async (
    Guid id,
    IAccountService service) =>
{
    await service.DeactivateAsync(id);

    return Results.NoContent();
})
.RequireAuthorization()
.WithName("DeactivateAccount");

app.Run();

public record ChangeEmailRequest(string NewEmail);