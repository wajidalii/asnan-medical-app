namespace Asnan.Application.Auth;

public interface IPasswordHasher
{
    /// <summary>Salted, versioned hash — the version prefix lets a future
    /// iteration-count/algorithm change roll out without invalidating
    /// existing hashes.</summary>
    string Hash(string password);

    bool Verify(string password, string hash);
}
