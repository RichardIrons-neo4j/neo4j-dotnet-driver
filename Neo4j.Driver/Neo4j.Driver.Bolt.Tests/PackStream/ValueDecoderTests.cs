// Copyright (c) "Neo4j"
// Neo4j Sweden AB [https://neo4j.com]
// 
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Buffers;
using FluentAssertions;
using Neo4j.Driver.Bolt.PackStream;
using Neo4j.Driver.Bolt.PackStream.Implementations.ValueDecoders;
using Neo4j.Driver.Bolt.Tests.TestHelpers;
using NUnit.Framework;

namespace Neo4j.Driver.Bolt.Tests.PackStream;

public class ValueDecoderTests
{
    [TestFixture]
    public class BooleanDecoderTests : UnitTestBase<BooleanDecoder>
    {
        [Test]
        public void HandlesTrueAndFalseMarkerBytes()
        {
            Subject.HandledMarkerBytes.Should().BeEquivalentTo([PackStreamMarker.True, PackStreamMarker.False]);
        }

        [Test]
        public void DecodesTrue()
        {
            var buffer = new ReadOnlySequence<byte>([PackStreamMarker.True]);

            var result = Subject.Decode(buffer);

            result.Value.BooleanValue.Should().BeTrue();
            result.BytesConsumed.Should().Be(1);
        }

        [Test]
        public void DecodesFalse()
        {
            var buffer = new ReadOnlySequence<byte>([PackStreamMarker.False]);

            var result = Subject.Decode(buffer);

            result.Value.BooleanValue.Should().BeFalse();
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
            var buffer = new ReadOnlySequence<byte>([0x00]);

            Action act = () => Subject.Decode(buffer);

            act.Should().Throw<InvalidOperationException>();
        }
    }

    [TestFixture]
    public class NullDecoderTests : UnitTestBase<NullDecoder>
    {
        [Test]
        public void HandlesNullMarkerByte()
        {
            Subject.HandledMarkerBytes.Should().BeEquivalentTo([PackStreamMarker.Null]);
        }

        [Test]
        public void DecodesNull()
        {
            var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Null]);

            var result = Subject.Decode(buffer);

            result.Value.IsNull.Should().BeTrue();
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
            var buffer = new ReadOnlySequence<byte>([0x00]);

            Action act = () => Subject.Decode(buffer);

            act.Should().Throw<InvalidOperationException>();
        }
    }

    [TestFixture]
    public class TinyIntDecoderTests : UnitTestBase<TinyIntDecoder>
    {
        [Test]
        public void HandlesCorrectMarkerBytes()
        {
            // 0x00-0x7F (positive) and 0xF0-0xFF (negative)
            Subject.HandledMarkerBytes.Should().BeEquivalentTo(new ByteRange(..0x80, 0xF0..0x100));
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

    [TestFixture]
    public class FloatDecoderTests : UnitTestBase<FloatDecoder>
    {
        [Test]
        public void HandlesFloat64MarkerByte()
        {
            Subject.HandledMarkerBytes.Should().BeEquivalentTo([PackStreamMarker.Float64]);
        }

        [Test]
        public void DecodesPositiveValue()
        {
            // 0x40 0x09 0x21 0xFB 0x54 0x44 0x2D 0x18 ~= pi
            var buffer = new ReadOnlySequence<byte>(
                [PackStreamMarker.Float64, 0x40, 0x09, 0x21, 0xFB, 0x54, 0x44, 0x2D, 0x18]);

            var result = Subject.Decode(buffer);

            result.Value.FloatValue.Should().BeApproximately(Math.PI, 1e-15);
            result.BytesConsumed.Should().Be(9);
        }

        [Test]
        public void DecodesNegativeValue()
        {
            // 0xC0 0x09 0x21 0xFB 0x54 0x44 0x2D 0x18 ~= -pi
            var buffer = new ReadOnlySequence<byte>(
                [PackStreamMarker.Float64, 0xC0, 0x09, 0x21, 0xFB, 0x54, 0x44, 0x2D, 0x18]);

            var result = Subject.Decode(buffer);

            result.Value.FloatValue.Should().BeApproximately(-Math.PI, 1e-15);
            result.BytesConsumed.Should().Be(9);
        }

        [Test]
        public void DecodesZero()
        {
            // 0x00 0x00 0x00 0x00 0x00 0x00 0x00 0x00 = 0.0
            var buffer = new ReadOnlySequence<byte>(
                [PackStreamMarker.Float64, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);

            var result = Subject.Decode(buffer);

            result.Value.FloatValue.Should().Be(0.0);
            result.BytesConsumed.Should().Be(9);
        }

        [Test]
        public void DecodesOne()
        {
            // 0x3F 0xF0 0x00 0x00 0x00 0x00 0x00 0x00 = 1.0
            var buffer = new ReadOnlySequence<byte>(
                [PackStreamMarker.Float64, 0x3F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);

            var result = Subject.Decode(buffer);

            result.Value.FloatValue.Should().Be(1.0);
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
                    [PackStreamMarker.Float64, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]); // Missing eighth value byte

            Action act = () => Subject.Decode(buffer);

            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void ThrowsOnUnknownMarker()
        {
            var buffer =
                new ReadOnlySequence<byte>(
                    [0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]); // Null marker, not Float64

            Action act = () => Subject.Decode(buffer);

            act.Should().Throw<InvalidOperationException>();
        }
    }

    [TestFixture]
    public class BytesDecoderTests : UnitTestBase<BytesDecoder>
    {
        [Test]
        public void HandlesBytes8Bytes16Bytes32MarkerBytes()
        {
            Subject.HandledMarkerBytes.Should()
                .BeEquivalentTo([PackStreamMarker.Bytes8, PackStreamMarker.Bytes16, PackStreamMarker.Bytes32]);
        }

        [Test]
        public void DecodesBytes8()
        {
            // Bytes8: marker + 1 byte length (3) + 3 bytes data
            var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Bytes8, 0x03, 0xAA, 0xBB, 0xCC]);

            var result = Subject.Decode(buffer);

            result.Value.BytesValue.ToArray().Should().BeEquivalentTo([0xAA, 0xBB, 0xCC]);
            result.BytesConsumed.Should().Be(5);
        }

        [Test]
        public void DecodesBytes8Empty()
        {
            // Bytes8: marker + 1 byte length (0)
            var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Bytes8, 0x00]);

            var result = Subject.Decode(buffer);

            result.Value.BytesValue.ToArray().Should().BeEmpty();
            result.BytesConsumed.Should().Be(2);
        }

        [Test]
        public void DecodesBytes16()
        {
            // Bytes16: marker + 2 byte length (3) + 3 bytes data
            var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Bytes16, 0x00, 0x03, 0xAA, 0xBB, 0xCC]);

            var result = Subject.Decode(buffer);

            result.Value.BytesValue.ToArray().Should().BeEquivalentTo([0xAA, 0xBB, 0xCC]);
            result.BytesConsumed.Should().Be(6);
        }

        [Test]
        public void DecodesBytes32()
        {
            // Bytes32: marker + 4 byte length (3) + 3 bytes data
            var buffer =
                new ReadOnlySequence<byte>([PackStreamMarker.Bytes32, 0x00, 0x00, 0x00, 0x03, 0xAA, 0xBB, 0xCC]);

            var result = Subject.Decode(buffer);

            result.Value.BytesValue.ToArray().Should().BeEquivalentTo([0xAA, 0xBB, 0xCC]);
            result.BytesConsumed.Should().Be(8);
        }

        [Test]
        public void ThrowsOnEmptyBuffer()
        {
            var buffer = ReadOnlySequence<byte>.Empty;

            Action act = () => Subject.Decode(buffer);

            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void ThrowsOnBytes8BufferTooShortForHeader()
        {
            var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Bytes8]);

            Action act = () => Subject.Decode(buffer);

            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void ThrowsOnBytes8BufferTooShortForData()
        {
            var buffer =
                new ReadOnlySequence<byte>([PackStreamMarker.Bytes8, 0x05, 0xAA, 0xBB]); // Says 5 bytes, only 2

            Action act = () => Subject.Decode(buffer);

            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void ThrowsOnBytes16BufferTooShortForHeader()
        {
            var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Bytes16, 0x00]);

            Action act = () => Subject.Decode(buffer);

            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void ThrowsOnBytes32BufferTooShortForHeader()
        {
            var buffer = new ReadOnlySequence<byte>([PackStreamMarker.Bytes32, 0x00, 0x00, 0x00]);

            Action act = () => Subject.Decode(buffer);

            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void ThrowsOnUnknownMarker()
        {
            var buffer = new ReadOnlySequence<byte>([0xC0, 0x03, 0xAA, 0xBB, 0xCC]);

            Action act = () => Subject.Decode(buffer);

            act.Should().Throw<InvalidOperationException>();
        }
    }

    [TestFixture]
    public class StringDecoderTests : UnitTestBase<StringDecoder>
    {
        [Test]
        public void HandlesAllStringMarkerBytes()
        {
            var validBytes = new ByteRange(0x80..0x90)
                .Add(PackStreamMarker.String8)
                .Add(PackStreamMarker.String16)
                .Add(PackStreamMarker.String32);
            
            Subject.HandledMarkerBytes.Should().BeEquivalentTo(validBytes);
        }

        [Test]
        public void DecodesTinyStringEmpty()
        {
            var result = Subject.Decode(new ReadOnlySequence<byte>([0x80]));

            result.Value.StringValue.ToString().Should().BeEmpty();
            result.BytesConsumed.Should().Be(1);
        }

        [Test]
        public void DecodesTinyString()
        {
            var result = Subject.Decode(new ReadOnlySequence<byte>([0x85, .."hello"u8]));

            result.Value.StringValue.ToString().Should().Be("hello");
            result.BytesConsumed.Should().Be(6);
        }

        [Test]
        public void DecodesString8()
        {
            var result = Subject.Decode(new ReadOnlySequence<byte>([PackStreamMarker.String8, 0x05, .."hello"u8]));

            result.Value.StringValue.ToString().Should().Be("hello");
            result.BytesConsumed.Should().Be(7);
        }

        [Test]
        public void DecodesString16()
        {
            var result = Subject.Decode(new ReadOnlySequence<byte>([PackStreamMarker.String16, 0x00, 0x05, .."hello"u8]));

            result.Value.StringValue.ToString().Should().Be("hello");
            result.BytesConsumed.Should().Be(8);
        }

        [Test]
        public void DecodesString32()
        {
            var result = Subject.Decode(new ReadOnlySequence<byte>([PackStreamMarker.String32, 0x00, 0x00, 0x00, 0x05, .."hello"u8]));

            result.Value.StringValue.ToString().Should().Be("hello");
            result.BytesConsumed.Should().Be(10);
        }

        [Test]
        public void DecodesUtf8MultiByteCharacters()
        {
            var result = Subject.Decode(new ReadOnlySequence<byte>([0x85, .."café"u8])); // é is 2 bytes

            result.Value.StringValue.ToString().Should().Be("café");
            result.BytesConsumed.Should().Be(6);
        }

        [Test]
        public void StringValueSupportsEnumeration()
        {
            var result = Subject.Decode(new ReadOnlySequence<byte>([0x82, .."hi"u8]));

            var runes = new List<string>();
            foreach (var rune in result.Value.StringValue)
            {
                runes.Add(rune.ToString());
            }

            runes.Should().BeEquivalentTo(["h", "i"]);
        }

        [Test]
        public void ThrowsOnEmptyBuffer()
        {
            Action act = () => Subject.Decode(ReadOnlySequence<byte>.Empty);

            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void ThrowsOnTruncatedData()
        {
            Action act = () => Subject.Decode(new ReadOnlySequence<byte>([0x85, 0x68, 0x69])); // Says 5 bytes, only 2

            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void ThrowsOnUnknownMarker()
        {
            Action act = () => Subject.Decode(new ReadOnlySequence<byte>([0xC0, .."hello"u8]));

            act.Should().Throw<InvalidOperationException>();
        }
    }
}
