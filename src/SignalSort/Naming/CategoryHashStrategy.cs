using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SignalSort.Naming;

/// <summary>
/// Naming strategy: Category + Hash (e.g., "Song_a1b2c3d4").
/// Uses a truncated MD5 hash of the file path for uniqueness.
/// </summary>
public class CategoryHashStrategy : INamingStrategy
{
    private const int HashLength = 8;

    public string GenerateName(string category, FileInfo file)
    {
        using var md5 = MD5.Create();
        var inputBytes = Encoding.UTF8.GetBytes(file.FullName);
        var hashBytes = md5.ComputeHash(inputBytes);
        
        // Take first 4 bytes and format as hex
        var hash = BitConverter.ToString(hashBytes[..4]).Replace("-", "").ToLowerInvariant();
        
        return $"{category}_{hash}";
    }
}
