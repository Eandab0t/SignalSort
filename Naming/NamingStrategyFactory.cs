using System;
using SmartFileClassifier.Models;

namespace SmartFileClassifier.Naming;

/// <summary>
/// Factory for creating naming strategy instances based on the NamingStyle enum.
/// </summary>
public static class NamingStrategyFactory
{
    public static INamingStrategy Create(NamingStyle style)
    {
        return style switch
        {
            NamingStyle.CategoryNumber => new CategoryNumberStrategy(),
            NamingStyle.CategoryHash => new CategoryHashStrategy(),
            NamingStyle.CategoryTimestamp => new CategoryTimestampStrategy(),
            NamingStyle.CategoryAdjectiveNoun => new CategoryAdjectiveNounStrategy(),
            _ => throw new NotSupportedException($"Naming style '{style}' is not supported.")
        };
    }
}
