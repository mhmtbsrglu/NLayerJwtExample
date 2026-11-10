namespace BusinessLogicLayer.Exceptions;

public class DuplicateEmailException(string email)
    : Exception($"Bu e-posta adresi zaten kullanılıyor: {email}");