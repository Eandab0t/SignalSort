using System;
using System.IO;
using SmartFileClassifier.Models;
using TagLib;

namespace SmartFileClassifier.Services;

/// <summary>
/// Extracts media metadata (duration, bitrate, channels, resolution) from audio and video files.
/// Uses TagLibSharp for cross-format support.
/// </summary>
public class MetadataExtractor : IDisposable
{
    private bool _disposed = false;

    /// <summary>
    /// Extracts metadata from an audio or video file.
    /// Returns null if the file cannot be read or is not a media file.
    /// </summary>
    public MediaMetadata? Extract(FileInfo file)
    {
        if (!file.Exists)
            return null;

        try
        {
            var metadata = new MediaMetadata();
            using var tagFile = TagLib.File.Create(file.FullName);

            // Duration
            if (tagFile.Properties != null)
            {
                metadata.Duration = tagFile.Properties.Duration;
                metadata.BitrateKbps = (int)(tagFile.Properties.AudioBitrate / 1000);
                metadata.SampleRateHz = tagFile.Properties.AudioSampleRate;
                metadata.Channels = tagFile.Properties.AudioChannels;

                // Video properties
                if (tagFile.Properties.VideoWidth > 0 && tagFile.Properties.VideoHeight > 0)
                {
                    metadata.Width = tagFile.Properties.VideoWidth;
                    metadata.Height = tagFile.Properties.VideoHeight;
                }
            }

            return metadata;
        }
        catch (Exception ex)
        {
            // Could not read file metadata (unsupported format, corrupted file, etc.)
            Log($"Warning: Could not read metadata for '{file.Name}': {ex.Message}");
            return null;
        }
    }

    public void Log(string message)
    {
        // Simple logging - can be connected to a logger
        Console.WriteLine($"[MetadataExtractor] {message}");
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _disposed = true;
        }
    }
}
