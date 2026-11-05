using System;

namespace SignalSort.Utilities;

/// <summary>
/// Generates random AdjectiveNoun combinations for file naming.
/// </summary>
public class AdjectiveNounGenerator
{
    private static readonly string[] Adjectives =
    {
        "Brave", "Clever", "Dapper", "Eager", "Fierce", "Gentle", "Happy", "Jolly",
        "Keen", "Loyal", "Merry", "Noble", "Proud", "Quick", "Rapid", "Silent",
        "Steady", "Swift", "Witty", "Zesty", "Bright", "Calm", "Bold", "Cool",
        "Crisp", "Dawn", "Deep", "Dusk", "Epic", "Fine", "Fresh", "Golden",
        "Grand", "Great", "Happy", "Hasty", "Huge", "Icy", "Kind", "Late",
        "Light", "Loud", "Major", "Mini", "Misty", "Mundane", "Narrow", "New",
        "Old", "Orange", "Polished", "Prime", "Quick", "Rapid", "Red", "Rich",
        "Rough", "Round", "Shiny", "Short", "Silent", "Silver", "Slim", "Slow",
        "Small", "Smart", "Soft", "Solid", "Solo", "Spark", "Spicy", "Spry",
        "Stark", "Steel", "Steep", "Sturdy", "Stylized", "Subtle", "Sudden", "Super",
        "Sweet", "Swift", "Tall", "Tender", "Thick", "Thin", "Tidy", "Tight",
        "Tiny", "Top", "Tough", "Twin", "Ultra", "Vast", "Vivid", "Warm",
        "Wary", "Wicked", "Wide", "Wild", "Willing", "Wise", "Witty", "Wobbly",
        "Young", "Zesty"
    };

    private static readonly string[] Nouns =
    {
        "Falcon", "Tiger", "Wolf", "Eagle", "Lion", "Bear", "Fox", "Hawk",
        "Panther", "Cobra", "Shark", "Whale", "Raven", "Stallion", "Panther", "Jaguar",
        "Mountain", "River", "Ocean", "Valley", "Forest", "Desert", "Island", "Canyon",
        "Thunder", "Lightning", "Storm", "Flame", "Shadow", "Echo", "Mist", "Frost",
        "Crystal", "Diamond", "Ruby", "Sapphire", "Emerald", "Pearl", "Onyx", "Topaz",
        "Phoenix", "Dragon", "Griffin", "Unicorn", "Sphinx", "Basilisk", "Chimera", "Pegasus",
        "Nimbus", "Stratus", "Cumulus", "Cyclone", "Typhoon", "Monsoon", "Hurricane", "Tornado",
        "Comet", "Asteroid", "Nova", "Galaxy", "Nebula", "Quasar", "Supernova", "Pulsar",
        "Vertex", "Apex", "Summit", "Peak", "Crest", "Horizon", "Dawn", "Dusk"
    };

    private static readonly Random Random = new();

    /// <summary>
    /// Generates a random AdjectiveNoun combination.
    /// </summary>
    public string Generate()
    {
        var adjective = Adjectives[Random.Next(Adjectives.Length)];
        var noun = Nouns[Random.Next(Nouns.Length)];
        return $"{adjective}{noun}";
    }
}
