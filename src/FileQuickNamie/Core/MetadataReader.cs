using System;
using System.Collections.Generic;
using System.IO;
using SmartFileClassifier.Models;
using TagLib;
using TagLib.Image;

namespace SmartFileClassifier.Core;

/// <summary>
/// Reads technical metadata from media and image files via TagLibSharp.
/// Single source of truth for metadata - used by both the scanner and the
/// classifier. Returns null rather than throwing when a file cannot be read.
/// </summary>
public class MetadataReader
{
    /// <summary>
    /// Extracts audio/video metadata (duration, bitrate, channels, resolution).
    /// </summary>
    public MediaMetadata? Read(FileInfo file)
    {
        if (!file.Exists)
            return null;

        try
        {
            using var tagFile = TagLib.File.Create(file.FullName);
            var metadata = new MediaMetadata();

            if (tagFile.Properties != null)
            {
                metadata.Duration = tagFile.Properties.Duration;
                metadata.BitrateKbps = (int)(tagFile.Properties.AudioBitrate / 1000);
                metadata.SampleRateHz = tagFile.Properties.AudioSampleRate;
                metadata.Channels = tagFile.Properties.AudioChannels;

                if (tagFile.Properties.VideoWidth > 0 && tagFile.Properties.VideoHeight > 0)
                {
                    metadata.Width = tagFile.Properties.VideoWidth;
                    metadata.Height = tagFile.Properties.VideoHeight;
                }

                return metadata;
            }

            return null;
        }
        catch (Exception)
        {
            // Unsupported format, corrupted file, or locked file - treat as "no metadata".
            return null;
        }
    }

    /// <summary>
    /// Reads image dimensions and the EXIF "date taken" when available.
    /// Falls back to null dimension values for unreadable images.
    /// </summary>
    public (int Width, int Height)? ReadImageDimensions(
        FileInfo file, List<ClassificationSignal> signals, out DateTime? dateTaken)
    {
        dateTaken = null;

        if (!file.Exists)
            return null;

        try
        {
            using var tagFile = TagLib.File.Create(file.FullName);

            // Images expose capture date via ImageTag.DateTime. TagLib.Image.File
            // is fully qualified here to avoid clashing with System.IO.File.
            if (tagFile is TagLib.Image.File imageFile)
            {
                var dt = imageFile.ImageTag?.DateTime;
                if (dt.HasValue)
                    dateTaken = dt;
            }

            var props = tagFile.Properties;
            if (props is { VideoWidth: > 0, VideoHeight: > 0 })
                return (props.VideoWidth, props.VideoHeight);
        }
        catch (Exception)
        {
            // Unsupported or corrupted image - fall back to size-based heuristics.
        }

        return null;
    }
}
