using System;
using System.IO;

namespace SignalSort.Naming;

/// <summary>
/// Naming strategy: Category + Number (e.g., "Song_001", "Song_002").
/// </summary>
public class CategoryNumberStrategy : INamingStrategy
{
    private readonly int _startingNumber;

    public CategoryNumberStrategy(int startingNumber = 1)
    {
        _startingNumber = startingNumber;
    }

    public string GenerateName(string category, FileInfo file)
    {
        var number = GetNextNumber();
        return $"{category}_{number:D3}";
    }

    private int GetNextNumber()
    {
        return _startingNumber + (_counter++);
    }

    private int _counter = 0;
}
