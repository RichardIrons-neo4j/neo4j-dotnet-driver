using System.Buffers;
using FluentAssertions;
using Neo4j.Driver.Bolt.PackStream;
using Neo4j.Driver.Bolt.PackStream.Implementations.ValueDecoders;
using NUnit.Framework;

namespace Neo4j.Driver.Bolt.Tests.PackStream.ValueDecoder;

[TestFixture]
public class Int16DecoderTests : UnitTestBase<Int16Decoder>
{
    [Test]
    public void HandlesInt16MarkerByte()
    {
        Subject.HandledMarkerBytes.Should().BeEquivalentTo([PackStreamMarker.Int16]);
    }

    [Test]
    public void DecodesPositiveValue()
    {
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Int16, 0x01, 0x00]); // 256

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(256);
        result.BytesConsumed.Should().Be(3);
    }

    [Test]
    public void DecodesNegativeValue()
    {
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Int16, 0x80, 0x00]); // -32768

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(-32768);
        result.BytesConsumed.Should().Be(3);
    }

    [Test]
    public void DecodesMaxValue()
    {
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Int16, 0x7F, 0xFF]); // 32767

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(32767);
        result.BytesConsumed.Should().Be(3);
    }

    [Test]
    public void DecodesMinusOne()
    {
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Int16, 0xFF, 0xFF]); // -1

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(-1);
        result.BytesConsumed.Should().Be(3);
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
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Int16, 0x00]); // Missing second value byte

        Action act = () => Subject.Decode(buffer);

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ThrowsOnUnknownMarker()
    {
        var buffer = new ReadOnlySequence<byte>([0xC0, 0x00, 0x00]); // Null marker, not Int16

        Action act = () => Subject.Decode(buffer);

        act.Should().Throw<InvalidOperationException>();
    }
}