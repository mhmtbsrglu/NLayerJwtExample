using BusinessLogicLayer.Abstracts;
using BusinessLogicLayer.Dtos;
using BusinessLogicLayer.Exceptions;
using CoreLayer.DataAccess.Abstracts;
using CoreLayer.Models;
using CoreLayer.Security.Hashing;
using CoreLayer.Utils.Security.Jwt;

namespace BusinessLogicLayer.Concretes;

public class AccountManager : IAccountService
{
    private readonly IAccountRepository _repository;
    private readonly IJwtTokenHelper _jwtTokenHelper;

    public AccountManager(
        IAccountRepository repository,
        IJwtTokenHelper jwtTokenHelper)
    {
        _repository = repository;
        _jwtTokenHelper = jwtTokenHelper;
    }

    public async Task<Account> RegisterAsync(RegisterDto dto)
    {
        var email = NormalizeEmail(dto.Email);

        if (string.IsNullOrWhiteSpace(dto.Password))
            throw new ArgumentException(
                "Şifre boş olamaz.",
                nameof(dto)
            );

        if (await EmailInUseAsync(email))
            throw new DuplicateEmailException(email);

        var passwordHash = PasswordHasher.HashPassword(dto.Password);

        var account = new Account(email, passwordHash);

        await _repository.CreateItemAsync(account);

        return account;
    }

    public async Task<AccessToken> LoginAsync(LoginDto dto)
    {
        var email = NormalizeEmail(dto.Email);

        if (string.IsNullOrWhiteSpace(dto.Password))
        {
            throw new InvalidCredentialsException();
        }

        var accounts = await _repository.ListItemsAsync();

        var account = accounts.FirstOrDefault(a =>
            a.Email.Equals(email, StringComparison.OrdinalIgnoreCase)
        );

        // Kullanıcı bulunamazsa dummy password verification yap.
        if (account is null)
        {
            PasswordHasher.VerifyPasswordDummy(dto.Password);

            throw new InvalidCredentialsException();
        }

        // Kullanıcının şifresini doğrula.
        var isPasswordValid = PasswordHasher.VerifyPassword(
            dto.Password,
            account.Password
        );

        // Kullanıcı pasifse veya şifresi yanlışsa aynı hatayı döndür.
        if (!isPasswordValid || !IsActive(account))
            throw new InvalidCredentialsException();

        // Şimdilik rol/claim ilişkisi henüz bağlanmadığı için boş.
        var operationClaims = new List<OperationClaim>();

        return _jwtTokenHelper.CreateAccessToken(
            account,
            operationClaims
        );
    }

    public async Task<Account?> GetByIdAsync(Guid id)
    {
        var account = await _repository.GetItemByIdAsync(id);

        return account is not null && IsActive(account)
            ? account
            : null;
    }

    public async Task<IEnumerable<Account>> ListActiveAsync()
    {
        var accounts = await _repository.ListItemsAsync();

        return accounts.Where(IsActive);
    }

    public async Task ChangeEmailAsync(Guid id, string newEmail)
    {
        var email = NormalizeEmail(newEmail);

        var account = await GetActiveOrThrowAsync(id);

        if (await EmailInUseAsync(email, exceptId: id))
            throw new DuplicateEmailException(email);

        account.ChangeEmail(email);

        await _repository.UpdateItemAsync(account);
    }

    public async Task DeactivateAsync(Guid id)
    {
        var account = await GetActiveOrThrowAsync(id);

        account.Deactivate();

        await _repository.UpdateItemAsync(account);
    }

    private async Task<bool> EmailInUseAsync(
        string email,
        Guid? exceptId = null)
    {
        var accounts = await _repository.ListItemsAsync();

        return accounts.Any(account =>
            IsActive(account) &&
            account.Id != exceptId &&
            account.Email.Equals(
                email,
                StringComparison.OrdinalIgnoreCase
            )
        );
    }

    private async Task<Account> GetActiveOrThrowAsync(Guid id)
    {
        var account = await GetByIdAsync(id);

        return account ?? throw new AccountNotFoundException(id);
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException(
                "E-posta boş olamaz.",
                nameof(email)
            );

        return email.Trim();
    }

    private static bool IsActive(Account account)
    {
        return !account.IsDeleted;
    }
}