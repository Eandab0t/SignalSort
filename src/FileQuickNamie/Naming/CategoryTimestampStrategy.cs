using System;
using System.IO;

namespace SmartFileClassifier.Naming;

/// <summary>
/// Naming strategy: Category + Timestamp (e.g., "Song_2024-01-15T143022").
/// Uses the file's creation time or current timestamp.
/// </summary>
public class CategoryTimestampStrategy : INamingStrategy
{
    public string GenerateName(string category, FileInfo file)
    {
        DateTime timestamp;
        
        try
        {
            timestamp = file.CreationTime;
        }
        catch
        {
            timestamp = DateTime.Now;
        }

        var formatted = timestamp.ToString("yyyy-MM-ddTHHmmss");
        return $"{category}_{formatted}";
    }
}
