using System.Buffers;
using FluentAssertions;
using Neo4j.Driver.Bolt.PackStream;
using Neo4j.Driver.Bolt.PackStream.Implementations.ValueDecoders;
using NUnit.Framework;

namespace Neo4j.Driver.Bolt.Tests.PackStream.ValueDecoder;

[TestFixture]
public class Int64DecoderTests : UnitTestBase<Int64Decoder>
{
    [Test]
    public void HandlesInt64MarkerByte()
    {
        Subject.HandledMarkerBytes.Should().BeEquivalentTo([PackStreamMarker.Int64]);
    }

    [Test]
    public void DecodesPositiveValue()
    {
        // 0x00 0x00 0x00 0x01 0x00 0x00 0x00 0x00 = 4294967296 (2^32)
        var buffer = new ReadOnlySequence<byte>(
            [PackStreamMarker.Int64, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00]);

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(4294967296L);
        result.BytesConsumed.Should().Be(9);
    }

    [Test]
    public void DecodesNegativeValue()
    {
        // 0x80 0x00 0x00 0x00 0x00 0x00 0x00 0x00 = -9223372036854775808 (long.MinValue)
        var buffer = new ReadOnlySequence<byte>(
            [PackStreamMarker.Int64, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(long.MinValue);
        result.BytesConsumed.Should().Be(9);
    }

    [Test]
    public void DecodesMaxValue()
    {
        // 0x7F 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF 0xFF = 9223372036854775807 (long.MaxValue)
        var buffer = new ReadOnlySequence<byte>(
            [PackStreamMarker.Int64, 0x7F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]);

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(long.MaxValue);
        result.BytesConsumed.Should().Be(9);
    }

    [Test]
    public void DecodesMinusOne()
    {
        var buffer = new ReadOnlySequence<byte>(
            [PackStreamMarker.Int64, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]);

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(-1L);
        result.BytesConsumed.Should().Be(9);
    }

    [Test]
    public void ThrowsOnEmptyBuffer()
    {
        var buffer = ReadOnlySequence<byte>.Empty;

        Action act = () => Subject.Decode(buffer);

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ThrowsOnBufferTooShort()
    {
        var buffer =
            new ReadOnlySequence<byte>(
                [PackStreamMarker.Int64, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]); // Missing eighth value byte

        Action act = () => Subject.Decode(buffer);

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ThrowsOnUnknownMarker()
    {
        var buffer =
            new ReadOnlySequence<byte>(
                [0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]); // Null marker, not Int64

        Action act = () => Subject.Decode(buffer);

        act.Should().Throw<InvalidOperationException>();
    }
}