using System.Buffers;
using FluentAssertions;
using Neo4j.Driver.Bolt.PackStream;
using Neo4j.Driver.Bolt.PackStream.Implementations.ValueDecoders;
using NUnit.Framework;

namespace Neo4j.Driver.Bolt.Tests.PackStream.ValueDecoder;

[TestFixture]
public class Int32DecoderTests : UnitTestBase<Int32Decoder>
{
    [Test]
    public void HandlesInt32MarkerByte()
    {
        Subject.HandledMarkerBytes.Should().BeEquivalentTo([PackStreamMarker.Int32]);
    }

    [Test]
    public void DecodesPositiveValue()
    {
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Int32, 0x00, 0x01, 0x00, 0x00]); // 65536

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(65536);
        result.BytesConsumed.Should().Be(5);
    }

    [Test]
    public void DecodesNegativeValue()
    {
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Int32, 0x80, 0x00, 0x00, 0x00]); // -2147483648

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(-2147483648);
        result.BytesConsumed.Should().Be(5);
    }

    [Test]
    public void DecodesMaxValue()
    {
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Int32, 0x7F, 0xFF, 0xFF, 0xFF]); // 2147483647

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(2147483647);
        result.BytesConsumed.Should().Be(5);
    }

    [Test]
    public void DecodesMinusOne()
    {
        var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Int32, 0xFF, 0xFF, 0xFF, 0xFF]); // -1

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(-1);
        result.BytesConsumed.Should().Be(5);
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
            new ReadOnlySequence<byte>([PackStreamMarker.Int32, 0x00, 0x00, 0x00]); // Missing fourth value byte

        Action act = () => Subject.Decode(buffer);

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ThrowsOnUnknownMarker()
    {
        var buffer = new ReadOnlySequence<byte>([0xC0, 0x00, 0x00, 0x00, 0x00]); // Null marker, not Int32

        Action act = () => Subject.Decode(buffer);

        act.Should().Throw<InvalidOperationException>();
    }
}