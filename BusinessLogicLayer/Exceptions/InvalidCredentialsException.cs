namespace BusinessLogicLayer.Exceptions;

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Email veya şifre hatalı.")
    {
    }
}