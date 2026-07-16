namespace FinalProject_Store.Application.Interfaces.Security
{
    public interface IPasswordHasher
    {
        string HashPassword(string password);

        bool VerifyPassword(
            string hashedPassword,
            string providedPassword);

        bool IsHashed(string password);
    }
}