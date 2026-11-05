using System.IO;

namespace SmartFileClassifier.Naming;

/// <summary>
/// Strategy interface for generating new file names.
/// Each implementation provides a different naming pattern.
/// </summary>
public interface INamingStrategy
{
    /// <summary>
    /// Generates a new file name based on the category and file info.
    /// </summary>
    /// <param name="category">The detected category (e.g., "SoundEffect", "Song").</param>
    /// <param name="file">The original file info.</param>
    /// <returns>The new file name without extension.</returns>
    string GenerateName(string category, FileInfo file);
}
