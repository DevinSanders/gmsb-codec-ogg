using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using NAudio.Wave;
using OggCodecPlugin;
using Xunit;

namespace OggCodecPlugin.Tests;

public class OggCodecPluginTests
{
    // 0.5 s, 44100 Hz, stereo sine tone — see fixtures/README.txt.
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "fixtures", "tone.ogg");

    private const int FixtureSampleRate = 44100;
    private const int FixtureChannels = 2;
    private static readonly TimeSpan FixtureDuration = TimeSpan.FromSeconds(0.5);

    // ── Declarations ────────────────────────────────────────────────

    [Fact]
    public void SupportedPatterns_ContainsOggExtensions()
    {
        var plugin = new OggCodecPlugin();
        plugin.SupportedPatterns.Should().Contain(new[] { ".ogg", ".oga" });
    }

    [Fact]
    public void SupportedContentTypes_ContainsOggMimeTypes()
    {
        var plugin = new OggCodecPlugin();
        plugin.SupportedContentTypes.Should()
            .Contain(new[] { "audio/ogg", "application/ogg", "audio/vorbis" });
    }

    [Fact]
    public void SupportsStreamInput_IsTrue()
    {
        new OggCodecPlugin().SupportsStreamInput.Should().BeTrue();
    }

    [Fact]
    public void Identity_FieldsAreStable()
    {
        var plugin = new OggCodecPlugin();
        plugin.Id.Should().Be("codec.ogg");
        plugin.Version.Should().NotBeNullOrWhiteSpace();
    }

    // ── Decode a fixture (file path) ────────────────────────────────

    [Fact]
    public void CreateStream_FromPath_DecodesWithExpectedFormat()
    {
        using var stream = new OggCodecPlugin().CreateStream(FixturePath);

        stream.WaveFormat.Encoding.Should().Be(WaveFormatEncoding.IeeeFloat);
        stream.WaveFormat.SampleRate.Should().Be(FixtureSampleRate);
        stream.WaveFormat.Channels.Should().Be(FixtureChannels);
        stream.CanSeek.Should().BeTrue("a real file is seekable");
    }

    [Fact]
    public void CreateStream_FromPath_ReadProducesSamples()
    {
        using var stream = new OggCodecPlugin().CreateStream(FixturePath);

        var buffer = new byte[8192];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);

        bytesRead.Should().BeGreaterThan(0);
        // IEEE float output → byte count is a whole number of floats.
        (bytesRead % sizeof(float)).Should().Be(0);
    }

    [Fact]
    public void CreateStream_FromPath_LengthMatchesClipDuration()
    {
        using var stream = new OggCodecPlugin().CreateStream(FixturePath);

        var reportedSeconds = stream.Length /
            (double)stream.WaveFormat.AverageBytesPerSecond;

        reportedSeconds.Should().BeApproximately(FixtureDuration.TotalSeconds, 0.1);
    }

    // ── Seek ────────────────────────────────────────────────────────

    [Fact]
    public void Position_RoundTrips()
    {
        using var stream = new OggCodecPlugin().CreateStream(FixturePath);

        // Seek to ~0.25 s in, aligned to a frame boundary.
        long target = stream.WaveFormat.AverageBytesPerSecond / 4;
        stream.Position = target;

        // Within one second's worth of bytes is plenty — Vorbis seeks to
        // the nearest granule, not the exact byte.
        stream.Position.Should().BeCloseTo(target, (ulong)stream.WaveFormat.AverageBytesPerSecond);
    }

    // ── Stream overload + ownership ─────────────────────────────────

    [Fact]
    public void CreateStream_FromStream_DecodesSameAsPath()
    {
        var bytes = File.ReadAllBytes(FixturePath);
        using var input = new MemoryStream(bytes);

        using var stream = new OggCodecPlugin().CreateStream(input, "audio/ogg");

        stream.WaveFormat.Encoding.Should().Be(WaveFormatEncoding.IeeeFloat);
        stream.WaveFormat.SampleRate.Should().Be(FixtureSampleRate);
        stream.WaveFormat.Channels.Should().Be(FixtureChannels);

        var buffer = new byte[8192];
        stream.Read(buffer, 0, buffer.Length).Should().BeGreaterThan(0);
    }

    [Fact]
    public void CreateStream_FromStream_DisposingWaveStreamDisposesInput()
    {
        var bytes = File.ReadAllBytes(FixturePath);
        var tracking = new TrackingStream(new MemoryStream(bytes));

        var stream = new OggCodecPlugin().CreateStream(tracking, "audio/ogg");
        tracking.Disposed.Should().BeFalse("ownership transfers but nothing is disposed yet");

        stream.Dispose();
        tracking.Disposed.Should().BeTrue("the WaveStream owns and disposes the input stream");
    }

    /// <summary>Delegating stream that records whether it was disposed,
    /// so the ownership-transfer contract can be asserted.</summary>
    private sealed class TrackingStream : Stream
    {
        private readonly Stream _inner;
        public bool Disposed { get; private set; }

        public TrackingStream(Stream inner) => _inner = inner;

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposed = true;
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
