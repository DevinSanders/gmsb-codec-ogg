using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using NVorbis;
using SoundBoard.PluginApi;

namespace OggCodecPlugin;

/// <summary>
/// <see cref="IAudioCodecPlugin"/> that adds OGG Vorbis (<c>.ogg</c>,
/// <c>.oga</c>) playback to Game Master Sound Board via the
/// <a href="https://github.com/NVorbis/NVorbis">NVorbis</a> managed
/// decoder. Pure-managed — no native libraries needed.
///
/// <para><b>Inter-plugin dispatch.</b> Implements the
/// <see cref="IAudioCodecPlugin.CreateStream(System.IO.Stream, string)"/>
/// overload so transport plugins (e.g. <c>codec.webstream</c>) can hand
/// a pre-opened HTTP stream here for decode. Declared MIME types are in
/// <see cref="SupportedContentTypes"/>.</para>
/// </summary>
public sealed class OggCodecPlugin : IAudioCodecPlugin
{
    public string Id => "codec.ogg";
    public string Name => "OGG Vorbis Codec";
    public string Description => "Adds .ogg / .oga playback support via the NVorbis managed decoder.";
    public string Version => PluginVersion.OfAssembly(typeof(OggCodecPlugin));
    public string Author => "Devin Sanders";

    public IEnumerable<string> SupportedPatterns => new[] { ".ogg", ".oga" };

    // "audio/ogg" is the modern RFC 5334 type; some servers emit
    // "application/ogg" — accept both. "audio/vorbis" is rare but
    // appears in older Icecast configs.
    public IEnumerable<string> SupportedContentTypes => new[]
    {
        "audio/ogg",
        "application/ogg",
        "audio/vorbis",
    };

    public bool SupportsStreamInput => true;

    public WaveStream CreateStream(string source) => new VorbisWaveStream(source);

    /// <summary>Decode an already-open <see cref="Stream"/> of Ogg-Vorbis
    /// bytes. Ownership transfers to the returned <see cref="WaveStream"/>;
    /// its <c>Dispose</c> closes the input. NVorbis validates the Ogg
    /// page framing itself, so <paramref name="formatHint"/> is advisory.</summary>
    public WaveStream CreateStream(Stream source, string formatHint)
        => new VorbisWaveStream(source);

    public void Initialize(IPluginContext context) { }
    public void Shutdown() { }
}

/// <summary>
/// Bridges <see cref="VorbisReader"/> (which produces float samples) to
/// <see cref="WaveStream"/> (which the codec plugin contract requires —
/// it's the type the host wraps in <c>GenericSeekableSampleProvider</c>).
/// Length / Position are computed from <c>VorbisReader.TotalTime</c> /
/// <c>TimePosition</c> against the bytes-per-second from the wave format
/// so seeking from the scrub slider works.
///
/// <para>Two constructors: file path (NVorbis owns the file) and Stream
/// (this class owns + disposes the stream alongside the reader).</para>
/// </summary>
internal sealed class VorbisWaveStream : WaveStream
{
    private readonly VorbisReader _reader;
    private readonly Stream? _owningStream;     // non-null when constructed from a Stream
    private readonly float[] _scratch = new float[16384];

    public override WaveFormat WaveFormat { get; }

    public VorbisWaveStream(string path)
    {
        _reader = new VorbisReader(path);
        _owningStream = null;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_reader.SampleRate, _reader.Channels);
    }

    /// <summary>Stream constructor for inter-plugin dispatch. Takes
    /// ownership; <see cref="Dispose(bool)"/> disposes both the reader
    /// and the input stream.
    ///
    /// <para>NVorbis's <c>VorbisReader(Stream)</c> defaults to
    /// <c>closeOnDispose: true</c>, so the reader already closes the
    /// stream when disposed. We keep our own reference and dispose it
    /// again (defensively, tolerating the double-dispose) in
    /// <see cref="Dispose(bool)"/>, since NVorbis's stream cleanup
    /// isn't guaranteed across versions.</para></summary>
    public VorbisWaveStream(Stream source)
    {
        _reader = new VorbisReader(source);
        _owningStream = source;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_reader.SampleRate, _reader.Channels);
    }

    /// <summary>Delegate seekability to the input Stream when present.
    /// File mode is always seekable; Stream mode reflects the transport.</summary>
    public override bool CanSeek => _owningStream?.CanSeek ?? true;

    public override long Length =>
        (long)(_reader.TotalTime.TotalSeconds * WaveFormat.AverageBytesPerSecond);

    public override long Position
    {
        get => (long)(_reader.TimePosition.TotalSeconds * WaveFormat.AverageBytesPerSecond);
        set
        {
            if (!CanSeek) return;
            var seconds = value / (double)WaveFormat.AverageBytesPerSecond;
            if (seconds < 0) seconds = 0;
            _reader.TimePosition = TimeSpan.FromSeconds(seconds);
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int floatsRequested = Math.Min(count / sizeof(float), _scratch.Length);
        if (floatsRequested <= 0) return 0;

        int floatsRead = _reader.ReadSamples(_scratch, 0, floatsRequested);
        int bytesRead = floatsRead * sizeof(float);
        Buffer.BlockCopy(_scratch, 0, buffer, offset, bytesRead);
        return bytesRead;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _reader.Dispose();
            // _reader (closeOnDispose: true) normally closes the stream
            // already; dispose again defensively in case it didn't.
            // Tolerate the resulting double-dispose.
            try { _owningStream?.Dispose(); } catch { }
        }
        base.Dispose(disposing);
    }
}
