using System.Security.Cryptography;
using System.Text;

namespace RandomRoom.Api.Auth;

/// <summary>
/// A secret the host keeps to get back in if they forget their PIN. Random and long enough (80 bits) that guessing is
/// hopeless, written in an alphabet without look-alike letters so it survives being copied by hand. Only a hash is
/// stored, like a PIN, so nobody with database access can read it.
/// </summary>
public static class RecoveryCode
{
    // Crockford's base32: no I, L, O or U, which are easily confused with 1, 0 or each other.
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int Length = 16;
    private const int GroupSize = 4;

    /// <summary>A new code such as "7QX4-K9M2-VB3H-T8RD".</summary>
    public static string Generate()
    {
        var chars = new char[Length];
        for (var i = 0; i < Length; i++) chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];

        var grouped = new StringBuilder();
        for (var i = 0; i < Length; i += GroupSize)
        {
            if (i > 0) grouped.Append('-');
            grouped.Append(chars, i, GroupSize);
        }
        return grouped.ToString();
    }

    /// <summary>
    /// Makes what a person typed comparable: case, dashes and spaces do not matter, and the letters Crockford's
    /// alphabet leaves out are read as the digits they look like (O as 0, I and L as 1).
    /// </summary>
    public static string Normalize(string? typed)
    {
        var builder = new StringBuilder();
        foreach (var ch in typed ?? "")
        {
            if (!char.IsAsciiLetterOrDigit(ch)) continue;
            builder.Append(char.ToUpperInvariant(ch) switch { 'O' => '0', 'I' or 'L' => '1', var c => c });
        }
        return builder.ToString();
    }
}
