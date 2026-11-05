using System;
using System.IO;
using SignalSort.Utilities;

namespace SignalSort.Naming;

/// <summary>
/// Naming strategy: Category + AdjectiveNoun (e.g., "Song_BrightFalcon").
/// Uses the random adjective/noun generator.
/// </summary>
public class CategoryAdjectiveNounStrategy : INamingStrategy
{
    private readonly AdjectiveNounGenerator _generator;

    public CategoryAdjectiveNounStrategy()
    {
        _generator = new AdjectiveNounGenerator();
    }

    public string GenerateName(string category, FileInfo file)
    {
        var adjectiveNoun = _generator.Generate();
        return $"{category}_{adjectiveNoun}";
    }
}
