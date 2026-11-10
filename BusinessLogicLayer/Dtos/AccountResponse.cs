using CoreLayer.Models;

namespace BusinessLogicLayer.Dtos;

/// <summary>
/// API-facing representation of an account.
/// Deliberately excludes the stored password hash.
/// </summary>
public sealed record AccountResponse(
    Guid Id,
    string Email,
    string Role,
    DateTime CreatedDate,
    DateTime? UpdatedDate,
    bool IsDeleted)
{
    public static AccountResponse FromAccount(Account account) => new(
        account.Id,
        account.Email,
        account.Role.ToString(),
        account.CreatedDate,
        account.UpdatedDate,
        account.IsDeleted);
}
