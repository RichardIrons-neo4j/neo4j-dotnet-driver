using System.Buffers;
using FluentAssertions;
using Neo4j.Driver.Bolt.PackStream;
using Neo4j.Driver.Bolt.PackStream.Implementations.ValueDecoders;
using NUnit.Framework;

namespace Neo4j.Driver.Bolt.Tests.PackStream.ValueDecoder;

[TestFixture]
public class Int8DecoderTests : UnitTestBase<Int8Decoder>
{
    [Test]
    public void HandlesInt8MarkerByte()
    {
        Subject.HandledMarkerBytes.Should().BeEquivalentTo([PackStreamMarker.Int8]);
    }

    [Test]
    public void DecodesPositiveValue()
    {
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Int8, 0x2A]); // 42

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(42);
        result.BytesConsumed.Should().Be(2);
    }

    [Test]
    public void DecodesNegativeValue()
    {
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Int8, 0x80]); // -128

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(-128);
        result.BytesConsumed.Should().Be(2);
    }

    [Test]
    public void DecodesMinusSeventeen()
    {
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Int8, 0xEF]); // -17

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(-17);
        result.BytesConsumed.Should().Be(2);
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
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Int8]); // Missing value byte

        Action act = () => Subject.Decode(buffer);

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ThrowsOnUnknownMarker()
    {
        var buffer = new ReadOnlySequence<byte>([0xC0, 0x00]); // Null marker, not Int8

        Action act = () => Subject.Decode(buffer);

        act.Should().Throw<InvalidOperationException>();
    }
}