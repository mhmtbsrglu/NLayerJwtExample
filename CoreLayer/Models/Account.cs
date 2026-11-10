using CoreLayer.Enums;
using CoreLayer.Models.Base.Concretes;

namespace CoreLayer.Models;


//Rich Domain Model
public class Account : BaseEntity
{
    public string Email { get; private set; } = string.Empty;
    public string Password { get; private set; } = string.Empty;
    public Role Role { get; private set; } = Role.User;

    public Account(string email, string password)
    {
        Email = email;
        Password = password;
    }

    public void ChangeEmail(string email)
    {
        Email = email;
        Touch();
    }

    public void ChangePassword(string password)
    {
        Password = password;
        Touch();
    }

    public void Deactivate()
    {
        IsDeleted = true;
        Touch();
    }

    internal void AssignRole(Role role)
    {
        Role = role;
        Touch();
    }

    private void Touch() => UpdatedDate = DateTime.UtcNow;
}