using System.Security.Cryptography;
using TransportPlatform.Application.Abstractions;

namespace TransportPlatform.Infrastructure.Services;

/// <summary>
/// Generates booking references like "BK-7F3K9Q" using an unambiguous alphabet
/// (no 0/O/1/I) and a cryptographically strong RNG.
/// </summary>
public sealed class ReferenceGenerator : IReferenceGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string NewBookingReference()
    {
        Span<char> chars = stackalloc char[6];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return $"BK-{new string(chars)}";
    }
}
