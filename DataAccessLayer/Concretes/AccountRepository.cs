
using CoreLayer.DataAccess.Abstracts;
using CoreLayer.Models;

namespace DataAccessLayer.Concretes;

public class AccountRepository : InMemoryDatabaseRepository<Account>, IAccountRepository
{
}