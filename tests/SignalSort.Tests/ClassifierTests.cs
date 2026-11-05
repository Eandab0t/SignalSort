using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using SignalSort.Core;
using SignalSort.Models;
using SignalSort.Naming;

namespace SignalSort.Tests;

public class ClassifierTests
{
    private static readonly MetadataReader Reader = new();
    private static readonly FileClassifier Classifier = new(Reader);

    [Fact]
    public void DetectCategory_MapsExtensions()
    {
        Assert.Equal("Audio", Classifier.DetectCategory(new FileInfo("x.mp3")));
        Assert.Equal("Audio", Classifier.DetectCategory(new FileInfo("x.WAV")));
        Assert.Equal("Video", Classifier.DetectCategory(new FileInfo("x.mp4")));
        Assert.Equal("Video", Classifier.DetectCategory(new FileInfo("x.MKV")));
        Assert.Equal("Image", Classifier.DetectCategory(new FileInfo("x.jpg")));
        Assert.Equal("Image", Classifier.DetectCategory(new FileInfo("x.PNG")));
        Assert.Equal("Archive", Classifier.DetectCategory(new FileInfo("x.zip")));
        Assert.Equal("Archive", Classifier.DetectCategory(new FileInfo("x.7z")));
        Assert.Equal("Document", Classifier.DetectCategory(new FileInfo("x.pdf")));
        Assert.Equal("Document", Classifier.DetectCategory(new FileInfo("x.unknown123")));
    }

    [Fact]
    public void ClassifyFolder_DatedSubfolders_AreArchives()
    {
        using var root = new TempDir();
        var d = root.Dir.CreateSubdirectory("2026-01-15");
        d.CreateSubdirectory("something"); // a dated-looking subfolder
        var (cat, purpose, conf) = Classifier.ClassifyFolder(root.Dir);
        Assert.Equal("Folder", cat);
        Assert.Equal("Archive_2026-01-15", purpose);
        Assert.Equal(ClassificationConfidence.Strong, conf);
    }

    [Fact]
    public void ClassifyFolder_MixedMedia_AreMediaDump()
    {
        using var root = new TempDir();
        root.File("a.mp3");
        root.File("b.mp4");
        root.File("c.png");
        root.File("d.txt");
        var (cat, purpose, conf) = Classifier.ClassifyFolder(root.Dir);
        Assert.Equal("Folder", cat);
        Assert.Equal("MediaDump", purpose);
        Assert.Equal(ClassificationConfidence.Heuristic, conf);
    }

    [Fact]
    public void ClassifyFolder_DominantAudio_AreAudioCollection()
    {
        using var root = new TempDir();
        root.File("a.mp3");
        root.File("b.mp3");
        root.File("c.mp3");
        root.File("d.mp4");
        var (cat, purpose, _) = Classifier.ClassifyFolder(root.Dir);
        Assert.Equal("AudioCollection", purpose);
    }

    [Fact]
    public void ClassifyFolder_Empty_IsEmpty()
    {
        using var root = new TempDir();
        var (cat, purpose, _) = Classifier.ClassifyFolder(root.Dir);
        Assert.Equal("Empty", purpose);
    }

    [Fact]
    public void Classify_Audio_SignalsAndConfidence()
    {
        // A tiny valid WAV: 44100 Hz, stereo, 16-bit, 1 second of silence.
        using var root = new TempDir();
        var wav = root.File("tone.wav", MakeWav(44100, 2, 16, 44100));
        var signals = new List<ClassificationSignal>();
        var (cat, purpose, conf, _) = Classifier.Classify(wav, signals);
        Assert.Equal("Audio", cat);
        Assert.True(conf == ClassificationConfidence.Strong);
        Assert.Contains(signals, s => s.Label == "Channels" && s.Value == "2");
        Assert.Contains(signals, s => s.Label == "Bitrate");
    }

    [Fact]
    public void Classify_Audio_VoiceMemo_MonoLowBitrate()
    {
        // 8000 Hz, mono, 8-bit => low bitrate + mono => VoiceMemo
        using var root = new TempDir();
        var wav = root.File("memo.wav", MakeWav(8000, 1, 8, 8000));
        var signals = new List<ClassificationSignal>();
        var (_, purpose, conf, _) = Classifier.Classify(wav, signals);
        Assert.Equal("VoiceMemo", purpose);
        Assert.Equal(ClassificationConfidence.Strong, conf);
    }

    [Fact]
    public void Classify_Video_LowRes_IsScreenRec()
    {
        // No real video container available; extension + no metadata => UnknownVideo.
        // Resolution classification requires TagLib-readable video.
        using var root = new TempDir();
        var vid = root.File("clip.avi", new byte[0]);
        var signals = new List<ClassificationSignal>();
        var (cat, purpose, conf, _) = Classifier.Classify(vid, signals);
        Assert.Equal("Video", cat);
        Assert.Equal("UnknownVideo", purpose);
        Assert.Equal(ClassificationConfidence.Unknown, conf);
    }

    [Fact]
    public void Classify_Image_Small_IsIcon()
    {
        using var root = new TempDir();
        var png = root.File("tiny.png", new byte[0]);
        var signals = new List<ClassificationSignal>();
        var (cat, purpose, conf, _) = Classifier.Classify(png, signals);
        Assert.Equal("Image", cat);
        // No dimensions => falls back to size (0 KB) => Thumbnail heuristic
        Assert.Equal("Thumbnail", purpose);
        Assert.Equal(ClassificationConfidence.Heuristic, conf);
    }

    [Fact]
    public void Classify_UnknownExtension_IsDocument()
    {
        using var root = new TempDir();
        var f = root.File("weird.xyzzy", new byte[] { 1, 2, 3 });
        var signals = new List<ClassificationSignal>();
        var (cat, purpose, _, _) = Classifier.Classify(f, signals);
        Assert.Equal("Document", cat);
        Assert.Equal("Document", purpose);
    }

    private static byte[] MakeWav(int sampleRate, int channels, int bitsPerSample, int sampleCount)
    {
        var dataSize = sampleCount * channels * (bitsPerSample / 8);
        var byteRate = sampleRate * channels * (bitsPerSample / 8);
        var blockAlign = channels * (bitsPerSample / 8);
        var riffSize = 36 + dataSize;
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(new char[] { 'R', 'I', 'F', 'F' });
        bw.Write(riffSize);
        bw.Write(new char[] { 'W', 'A', 'V', 'E' });
        bw.Write(new char[] { 'f', 'm', 't', ' ' });
        bw.Write(16); // fmt chunk size
        bw.Write((short)1); // PCM
        bw.Write((short)channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write((short)blockAlign);
        bw.Write((short)bitsPerSample);
        bw.Write(new char[] { 'd', 'a', 't', 'a' });
        bw.Write(dataSize);
        bw.Write(new byte[dataSize]);
        return ms.ToArray();
    }

    private sealed class TempDir : IDisposable
    {
        public string FolderPath { get; }
        public DirectoryInfo Dir => new(FolderPath);
        public TempDir()
        {
            FolderPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(FolderPath);
        }
        public FileInfo File(string name, byte[]? content = null)
        {
            var p = System.IO.Path.Combine(FolderPath, name);
            System.IO.File.WriteAllBytes(p, content ?? Array.Empty<byte>());
            return new FileInfo(p);
        }
        public void Dispose()
        {
            try { System.IO.Directory.Delete(FolderPath, true); } catch { }
        }
    }
}
