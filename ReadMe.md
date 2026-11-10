# NLayerJwtExample

A secure JWT-based authentication and authorization implementation built with ASP.NET Core.

This project demonstrates how to implement stateless authentication using **JSON Web Tokens (JWT)** and asymmetric cryptography with **RS256 (RSA + SHA-256)**.

It uses an RSA key pair to separate token signing from token verification. The private key is responsible for signing access tokens, while the public key is used to validate their signatures.

The project also includes password hashing, credential verification, configurable token expiration, and JWT Bearer authentication.

## Technologies

- C#
- ASP.NET Core Web API
- JSON Web Token (JWT)
- RS256 (RSA + SHA-256)
- RSA 3072-bit
- PBKDF2-SHA256
- HMAC-SHA256
- OpenSSL
- Dependency Injection

## Authentication Flow

### Registration

When a user creates an account:

1. The API receives the registration request.
2. The email address is normalized.
3. The password is securely hashed using PBKDF2-SHA256.
4. The resulting password hash is stored.
5. The account is created.

Plain-text passwords are never stored.

### Login

When a user attempts to authenticate:

1. The API receives the login credentials.
2. The account is retrieved using the provided email address.
3. The submitted password is verified against the stored password hash.
4. Invalid credentials result in an authentication failure.
5. If verification succeeds, an access token is generated.
6. The access token is returned to the client.

### Access Token Validation

For authenticated requests:

1. The client sends the access token in the `Authorization` header.
2. ASP.NET Core JWT Bearer middleware extracts the token.
3. The token signature is verified using the RSA public key.
4. Issuer, audience, expiration, and signing algorithm are validated.
5. If validation succeeds, the authenticated user principal is created.
6. Authorization rules determine whether access to the requested resource is permitted.

Example:

```http
Authorization: Bearer YOUR_ACCESS_TOKEN
```

## JWT Security – RS256

This project uses RS256, an asymmetric digital signature algorithm based on RSA and SHA-256.

Unlike HS256, which relies on a shared secret for both signing and verification, RS256 uses two mathematically related keys:

- **Private Key:** Signs access tokens.
- **Public Key:** Verifies access token signatures.

### Why RS256?

RS256 provides several security advantages:

- Signing and verification responsibilities are separated.
- Services can verify tokens without having access to the private signing key.
- The private key can remain isolated from resource servers.
- It is suitable for distributed applications and service-to-service authentication.

### Private Key

The private key is used to generate JWT signatures.

It must remain confidential and should never be distributed to clients or committed to version control.

### Public Key

The public key is used to verify JWT signatures.

It can be distributed to services that need to validate tokens without exposing the private signing key.

### Important: JWT Is Not Encrypted

JWT signatures provide integrity and authenticity, not confidentiality.

The token payload can be decoded by anyone who obtains the token. Never include passwords, private keys, or other sensitive information in JWT claims.

## RSA Key Generation

OpenSSL is used to generate the RSA key pair.

### Requirements

Install OpenSSL and make sure the `openssl` command is available in your terminal.

Verify the installation:

```bash
openssl version
```

### 1. Generate the Private Key

Generate a 3072-bit RSA private key:

```bash
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -out private.pem
```

### 2. Generate the Public Key

Derive the public key from the private key:

```bash
openssl pkey -pubout -in private.pem -out public.pem
```

The generated files are:

- `private.pem`
- `public.pem`

### 3. Verify the Keys

Inspect the private key:

```bash
openssl pkey -in private.pem -text -noout
```

Inspect the public key:

```bash
openssl pkey -pubin -in public.pem -text -noout
```

Verify that both keys belong to the same key pair:

```bash
openssl pkey -in private.pem -pubout -outform PEM | openssl sha256

openssl pkey -pubin -in public.pem -pubout -outform PEM | openssl sha256
```

Both SHA-256 fingerprints should match.

## Configuration

JWT settings are defined in `appsettings.json`.

Example:

```json
{
  "TokenOptions": {
    "Issuer": "https://localhost:7001",
    "Audience": "https://localhost:7001",
    "AccessTokenExpiration": 15,
    "PrivateKeyPath": "Keys/private.pem",
    "PublicKeyPath": "Keys/public.pem"
  }
}
```

| Property | Description |
|---|---|
| `Issuer` | Identifies the token issuer. |
| `Audience` | Identifies the intended token recipient. |
| `AccessTokenExpiration` | Access token lifetime in minutes. |
| `PrivateKeyPath` | Location of the RSA private key. |
| `PublicKeyPath` | Location of the RSA public key. |

The issuer and audience values must match between token generation and token validation.

Key paths must resolve correctly from the application's configured content root.

## Token Claims

The JWT contains claims that identify the authenticated account and provide additional authorization information.

The token generation process includes:

| Claim | Description |
|---|---|
| `jti` | Unique identifier for the token. |
| `NameIdentifier` | Identifies the authenticated account. |
| `Role` | Represents the account's authorization roles. |

Role claims can be used by ASP.NET Core authorization mechanisms to restrict access to protected endpoints.

## Authentication Configuration

JWT Bearer authentication validates incoming access tokens using the RSA public key.

The validation configuration includes:

```csharp
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

    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],

    ClockSkew = TimeSpan.FromSeconds(30)
};
```

The application middleware must include:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

Authentication must run before authorization.

Protected endpoints can require authentication using:

```csharp
.RequireAuthorization();
```

Public endpoints such as registration and login can use:

```csharp
.AllowAnonymous();
```

## Password Security

Password storage is handled separately from JWT signing.

The password hashing implementation uses:

- PBKDF2 with SHA-256.
- 600,000 iterations.
- A unique salt for each password.
- An additional pepper.
- HMAC-SHA256 for additional hash protection.

The implementation also supports password verification, dummy verification, and rehash checks.

Password hashing is intentionally computationally expensive to increase the cost of offline password-guessing attacks.

## Security Considerations

### Private Key Protection

- Never commit `private.pem` to version control.
- Never expose private keys through API responses.
- Never include private keys in frontend applications.
- Use a secure secret-management solution in production.
- Restrict filesystem permissions for private key files.
- Rotate the key pair if the private key is compromised.

### Token Security

- Use HTTPS for all authentication requests.
- Keep access token lifetimes limited.
- Validate issuer and audience.
- Validate token expiration.
- Restrict accepted signing algorithms.
- Never trust claims before signature validation.
- Avoid storing sensitive information inside JWT payloads.

### Git Ignore

Add the following entries to the root `.gitignore`:

```gitignore
# RSA private keys
Keys/private.pem
Keys/*.key

# Environment files
.env
.env.*
!.env.example

# .NET build outputs
[Bb]in/
[Oo]bj/

# IDE
.idea/
.vs/
*.user
*.suo
```

The public key can be committed to version control if needed. The private key must never be committed.

If a private key has already been pushed to a remote repository, removing it from Git tracking is not sufficient. Generate a new key pair and replace the compromised key.

## API Endpoints

The following endpoints represent the account authentication operations:

| Method | Endpoint | Description | Access |
|---|---|---|---|
| POST | `/accounts/register` | Creates a new account. | Anonymous |
| POST | `/accounts/login` | Authenticates an account and returns an access token. | Anonymous |

### Login Request

```json
{
  "email": "user@example.com",
  "password": "YourPassword"
}
```

### Authenticated Request

After successfully logging in, include the returned access token in subsequent requests:

```http
GET /profile
Authorization: Bearer YOUR_ACCESS_TOKEN
```

Actual endpoint paths depend on the application's route configuration.

## Getting Started

### 1. Clone the Repository

```bash
git clone <repository-url>
cd NLayerJwtExample
```

### 2. Generate RSA Keys

Create the keys directory and generate the key pair:

```bash
mkdir Keys

openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -out Keys/private.pem

openssl pkey -pubout -in Keys/private.pem -out Keys/public.pem
```

### 3. Configure JWT Settings

Update the `TokenOptions` section in `PresentationLayer/appsettings.json`.

Make sure the issuer, audience, and RSA key paths are configured correctly.

### 4. Restore Dependencies

```bash
dotnet restore
```

### 5. Build the Project

```bash
dotnet build
```

### 6. Run the Application

```bash
dotnet run --project PresentationLayer
```

## License

This project is intended for educational and development purposes. Add your preferred license before distributing or publishing the project.