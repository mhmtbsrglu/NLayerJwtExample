namespace BusinessLogicLayer.Exceptions;

public class AccountNotFoundException(Guid id)
    : Exception($"Hesap bulunamadı: {id}");