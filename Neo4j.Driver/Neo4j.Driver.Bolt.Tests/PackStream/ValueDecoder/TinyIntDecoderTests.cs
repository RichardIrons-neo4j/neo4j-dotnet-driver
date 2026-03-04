using System.Buffers;
using FluentAssertions;
using Neo4j.Driver.Bolt.PackStream.Implementations.ValueDecoders;
using Neo4j.Driver.Bolt.Tests.TestHelpers;
using NUnit.Framework;

namespace Neo4j.Driver.Bolt.Tests.PackStream.ValueDecoder;

[TestFixture]
public class TinyIntDecoderTests : UnitTestBase<TinyIntDecoder>
{
    [Test]
    public void HandlesCorrectMarkerBytes()
    {
        // 0x00-0x7F (positive) and 0xF0-0xFF (negative)
        var validBytes = new ByteArrayBuilder().Range(..0x80).Range(0xF0..0x100);
        Subject.HandledMarkerBytes.Should().BeEquivalentTo(validBytes);
    }

    [Test]
    public void DecodesZero()
    {
        var buffer = new ReadOnlySequence<byte>([0x00]);

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(0);
        result.BytesConsumed.Should().Be(1);
    }

    [Test]
    public void DecodesPositiveValue()
    {
        var buffer = new ReadOnlySequence<byte>([0x2A]); // 42

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(42);
        result.BytesConsumed.Should().Be(1);
    }

    [Test]
    public void DecodesMaxPositiveValue()
    {
        var buffer = new ReadOnlySequence<byte>([0x7F]); // 127

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(127);
        result.BytesConsumed.Should().Be(1);
    }

    [Test]
    public void DecodesMinusOne()
    {
        var buffer = new ReadOnlySequence<byte>([0xFF]); // -1

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(-1);
        result.BytesConsumed.Should().Be(1);
    }

    [Test]
    public void DecodesMinNegativeValue()
    {
        var buffer = new ReadOnlySequence<byte>([0xF0]); // -16

        var result = Subject.Decode(buffer);

        result.Value.IntValue.Should().Be(-16);
        result.BytesConsumed.Should().Be(1);
    }

    [Test]
    public void ThrowsOnEmptyBuffer()
    {
        var buffer = ReadOnlySequence<byte>.Empty;

        Action act = () => Subject.Decode(buffer);

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ThrowsOnUnknownMarker()
    {
        var buffer = new ReadOnlySequence<byte>([0xC0]); // Null marker, not a TinyInt

        Action act = () => Subject.Decode(buffer);

        act.Should().Throw<InvalidOperationException>();
    }
}