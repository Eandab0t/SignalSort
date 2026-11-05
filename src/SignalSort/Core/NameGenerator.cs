using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SignalSort.Models;
using SignalSort.Naming;
using SignalSort.Utilities;

namespace SignalSort.Core;

/// <summary>
/// Generates new names for a whole batch of classifications.
///
/// Sequence numbers (CategoryPrefixed, CategoryNumber) are assigned per
/// (purpose, date) group across the entire batch, so regenerating names never
/// produces duplicates within one folder - the old strategy instances reset
/// their counters on every style change.
/// </summary>
public class NameGenerator
{
    /// <summary>
    /// Assigns NewName to every item in the batch according to the style.
    /// </summary>
    public void AssignNames(
        IEnumerable<FileClassification> items,
        NamingStyle style,
        bool preserveExtension)
    {
        // Per (purpose, date) sequence counters shared across the batch.
        var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var baseName = style switch
            {
                NamingStyle.CategoryPrefixed => GeneratePrefixed(item, counters),
                NamingStyle.CategoryNumber => GenerateNumbered(item, counters),
                NamingStyle.CategoryHash => Sanitize(item.Purpose) + "_" + ShortHash(item.FullPath),
                NamingStyle.CategoryTimestamp => GenerateTimestamped(item, counters),
                NamingStyle.CategoryAdjectiveNoun => GenerateAdjectiveNoun(item, counters),
                _ => Sanitize(item.Purpose)
            };

            if (preserveExtension && !string.IsNullOrEmpty(item.Extension))
                baseName += item.Extension;

            // File names must never contain invalid characters, even if the
            // purpose string somehow contains them.
            item.NewName = RemoveInvalidChars(baseName);
        }
    }

    /// <summary>
    /// Default style: VoiceMemo_20261104_001, ScreenRec_20261104_002 ...
    /// Sequence restarts per (purpose, date) pair.
    /// </summary>
    private static string GeneratePrefixed(FileClassification item, Dictionary<string, int> counters)
    {
        var date = GetEffectiveDate(item);
        var key = item.Purpose + "|" + date.ToString("yyyyMMdd");
        var n = NextCounter(counters, key);
        return $"{Sanitize(item.Purpose)}_{date:yyyyMMdd}_{n:D3}";
    }

    /// <summary>
    /// Song_001, Song_002 - sequence restarts per purpose.
    /// </summary>
    private static string GenerateNumbered(FileClassification item, Dictionary<string, int> counters)
    {
        var n = NextCounter(counters, item.Purpose);
        return $"{Sanitize(item.Purpose)}_{n:D3}";
    }

    /// <summary>
    /// Song_2026-01-15T143022 - uses the best-known capture date.
    /// Adds a sequence suffix when two items share the same second,
    /// instead of silently colliding.
    /// </summary>
    private static string GenerateTimestamped(FileClassification item, Dictionary<string, int> counters)
    {
        var date = GetEffectiveDate(item);
        var stamp = date.ToString("yyyy-MM-ddTHHmmss");
        var key = item.Purpose + "|" + stamp;
        var n = NextCounter(counters, key);
        var name = $"{Sanitize(item.Purpose)}_{stamp}";
        return n == 1 ? name : $"{name}_{n}";
    }

    /// <summary>
    /// Song_BrightFalcon - creative names stay unique within the batch by
    /// appending the batch sequence number.
    /// </summary>
    private static string GenerateAdjectiveNoun(FileClassification item, Dictionary<string, int> counters)
    {
        var generator = new AdjectiveNounGenerator();
        var key = item.Purpose;
        var n = NextCounter(counters, key);
        return $"{Sanitize(item.Purpose)}_{generator.Generate()}_{n:D3}";
    }

    /// <summary>
    /// Best-known capture date: EXIF/media date, then file system dates,
    /// then today. Never returns null so names stay predictable.
    /// </summary>
    public static DateTime GetEffectiveDate(FileClassification item)
    {
        if (item.DateTaken.HasValue)
            return item.DateTaken.Value;

        try
        {
            if (item.Kind == ItemKind.Folder && Directory.Exists(item.FullPath))
                return Directory.GetLastWriteTime(item.FullPath);
            if (File.Exists(item.FullPath))
                return File.GetLastWriteTime(item.FullPath);
        }
        catch
        {
            // Fall through to today.
        }

        return DateTime.Today;
    }

    private static int NextCounter(Dictionary<string, int> counters, string key)
    {
        counters.TryGetValue(key, out var current);
        current++;
        counters[key] = current;
        return current;
    }

    /// <summary>
    /// Purpose strings can contain characters that are unsafe in file names
    /// (e.g. "Archive_2026-01-15" is fine, but future purposes may not be).
    /// </summary>
    public static string Sanitize(string purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose))
            return "File";

        var invalid = Path.GetInvalidFileNameChars();
        var chars = purpose.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var result = new string(chars).Trim();

        return result.Length == 0 ? "File" : result;
    }

    private static string ShortHash(string input)
    {
        // Stable, dependency-free 8-char hash (FNV-1a) - deterministic across runs.
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in input)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash.ToString("x8");
        }
    }

    private static string RemoveInvalidChars(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}
