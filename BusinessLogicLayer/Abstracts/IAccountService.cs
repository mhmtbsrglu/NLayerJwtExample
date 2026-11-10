using BusinessLogicLayer.Dtos;
using CoreLayer.Models;
using CoreLayer.Utils.Security.Jwt;

namespace BusinessLogicLayer.Abstracts;

public interface IAccountService
{
    Task<Account> RegisterAsync(RegisterDto dto);

    Task<AccessToken> LoginAsync(LoginDto dto);

    Task<Account?> GetByIdAsync(Guid id);

    Task<IEnumerable<Account>> ListActiveAsync();

    Task ChangeEmailAsync(Guid id, string newEmail);

    Task DeactivateAsync(Guid id);
}