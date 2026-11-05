using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using SmartFileClassifier.Core;
using SmartFileClassifier.Models;

namespace FileQuickNamie.Tests;

public class NameGeneratorTests
{
    private static FileClassification Item(string name, string purpose, DateTime? date = null, string? extension = null, ItemKind kind = ItemKind.File)
    {
        var ext = extension ?? System.IO.Path.GetExtension(name);
        return new()
        {
            OriginalName = name,
            Category = "Audio",
            Purpose = purpose,
            FullPath = Path.Combine(@"C:\Music", name),
            Extension = ext,
            DateTaken = date,
            Kind = kind,
        };
    }

    [Fact]
    public void CategoryPrefixed_UsesPurpose_Date_Sequence()
    {
        var items = new[]
        {
            Item("a.m4a", "VoiceMemo", new DateTime(2026, 11, 4)),
            Item("b.m4a", "VoiceMemo", new DateTime(2026, 11, 4)),
            Item("c.mp4", "ScreenRec", new DateTime(2026, 11, 4)),
        };
        new NameGenerator().AssignNames(items, NamingStyle.CategoryPrefixed, true);

        Assert.Equal("VoiceMemo_20261104_001.m4a", items[0].NewName);
        Assert.Equal("VoiceMemo_20261104_002.m4a", items[1].NewName);
        Assert.Equal("ScreenRec_20261104_001.mp4", items[2].NewName);
    }

    [Fact]
    public void CategoryPrefixed_SequenceRestartsPerDay()
    {
        var items = new[]
        {
            Item("a.m4a", "Song", new DateTime(2026, 11, 4)),
            Item("b.m4a", "Song", new DateTime(2026, 11, 5)),
        };
        new NameGenerator().AssignNames(items, NamingStyle.CategoryPrefixed, true);
        Assert.Equal("Song_20261104_001.m4a", items[0].NewName);
        Assert.Equal("Song_20261105_001.m4a", items[1].NewName);
    }

    [Fact]
    public void CategoryPrefixed_FallsBackToTodayWhenNoDate()
    {
        var items = new[] { Item("a.m4a", "Song") };
        new NameGenerator().AssignNames(items, NamingStyle.CategoryPrefixed, true);
        var expected = $"Song_{DateTime.Today:yyyyMMdd}_001.m4a";
        Assert.Equal(expected, items[0].NewName);
    }

    [Fact]
    public void CategoryNumber_SequencesPerPurpose()
    {
        var items = new[]
        {
            Item("a.mp3", "Song"),
            Item("b.mp3", "Song"),
            Item("c.mp3", "VoiceMemo"),
        };
        new NameGenerator().AssignNames(items, NamingStyle.CategoryNumber, true);
        Assert.Equal("Song_001.mp3", items[0].NewName);
        Assert.Equal("Song_002.mp3", items[1].NewName);
        Assert.Equal("VoiceMemo_001.mp3", items[2].NewName);
    }

    [Fact]
    public void CategoryTimestamp_UsesDateTaken()
    {
        var items = new[]
        {
            Item("a.jpg", "Photo", new DateTime(2026, 1, 15, 14, 30, 22)),
            Item("b.jpg", "Photo", new DateTime(2026, 1, 15, 14, 30, 22)),
        };
        new NameGenerator().AssignNames(items, NamingStyle.CategoryTimestamp, true);
        Assert.Equal("Photo_2026-01-15T143022.jpg", items[0].NewName);
        Assert.Equal("Photo_2026-01-15T143022_2.jpg", items[1].NewName);
    }

    [Fact]
    public void CategoryHash_Deterministic()
    {
        var items = new[]
        {
            Item("a.mp3", "Song"),
            Item("b.mp3", "Song"),
        };
        new NameGenerator().AssignNames(items, NamingStyle.CategoryHash, true);
        Assert.Equal($"Song_{NameGeneratorTests.Hash("C:\\Music\\a.mp3")}.mp3", items[0].NewName);
        Assert.Equal($"Song_{Hash("C:\\Music\\b.mp3")}.mp3", items[1].NewName);
        Assert.NotEqual(items[0].NewName, items[1].NewName);
    }

    private static string Hash(string input)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (var c in input)
            {
                h ^= c;
                h *= 16777619;
            }
            return h.ToString("x8");
        }
    }

    [Fact]
    public void CategoryAdjectiveNoun_UniqueWithinBatch()
    {
        var items = new[]
        {
            Item("a.mp3", "Song"),
            Item("b.mp3", "Song"),
        };
        new NameGenerator().AssignNames(items, NamingStyle.CategoryAdjectiveNoun, true);
        Assert.StartsWith("Song_", items[0].NewName);
        Assert.StartsWith("Song_", items[1].NewName);
        Assert.NotEqual(items[0].NewName, items[1].NewName);
    }

    [Fact]
    public void Sanitize_RemovesInvalidCharacters()
    {
        var gen = new NameGenerator();
        // Use reflection-free check via public surface: build items whose purpose
        // contains invalid filename chars.
        var items = new[] { Item("x.mp3", "So?ng|<>:") };
        new NameGenerator().AssignNames(items, NamingStyle.CategoryNumber, true);
        Assert.DoesNotMatch(@"[<>:/\?\*\|]", items[0].NewName);
    }

    [Fact]
    public void PreserveExtension_KeepsExtension()
    {
        var items = new[] { Item("a.MP3", "Song") };
        new NameGenerator().AssignNames(items, NamingStyle.CategoryPrefixed, true);
        Assert.EndsWith(".MP3", items[0].NewName);
    }

    [Fact]
    public void NoExtension_WhenNotPreserved()
    {
        var items = new[] { Item("a.mp3", "Song", extension: "") };
        new NameGenerator().AssignNames(items, NamingStyle.CategoryPrefixed, false);
        Assert.False(items[0].NewName.EndsWith(".mp3"));
    }

    [Fact]
    public void Unicode_NamesHandled()
    {
        var items = new[] { Item("café_ñ.mp3", "Song") };
        new NameGenerator().AssignNames(items, NamingStyle.CategoryPrefixed, true);
        Assert.Equal($"Song_{DateTime.Today:yyyyMMdd}_001.mp3", items[0].NewName);
    }

    [Fact]
    public void Folder_KindWorks()
    {
        var item = new FileClassification
        {
            OriginalName = "2026-01-15",
            Category = "Folder",
            Purpose = "Archive",
            FullPath = @"C:\Music\2026-01-15",
            Extension = "",
            Kind = ItemKind.Folder,
        };
        new NameGenerator().AssignNames(new[] { item }, NamingStyle.CategoryPrefixed, false);
        Assert.Equal($"Archive_{DateTime.Today:yyyyMMdd}_001", item.NewName);
    }
}
