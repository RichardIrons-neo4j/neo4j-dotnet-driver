using System.Buffers;
using System.IO.Pipelines;
using FluentAssertions;
using Neo4j.Driver.Bolt.Tests.TestHelpers;
using Neo4j.Driver.Bolt.Transport;
using NUnit.Framework;

namespace Neo4j.Driver.Bolt.Tests;

[TestFixture]
public class ChunkAssemblerTests
{
    [Test]
    public async Task AssemblesCorrectlyFormedMessage()
    {
        // Arrange
        byte[] bytes =
        [
            0, 10, // there are 10 bytes in this message
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9 // the message payload
        ];

        var sequence = new ReadOnlySequence<byte>(bytes);
        var reader = PipeReader.Create(sequence);
        var assembler = new ChunkAssembler();
        byte[] expectedBytes = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

        // Act
        var messages = await assembler.ReadMessagesAsync(reader).ToListAsync();

        // Assert
        messages.Should().HaveCount(1);
        messages[0].ToArray().Should().BeEquivalentTo(expectedBytes);
    }

    [Test]
    public async Task CorrectlyAssemblesChunkedMessage()
    {
        // Arrange
        byte[][] chunks =
        [
            [
                0x00, 0x0A, // there are 10 bytes in this message
                0x00, 0x01, 0x02, 0x03, 0x04, // first chunk of the message payload
            ],
            [
                0x05, 0x06, 0x07, 0x08, 0x09, // second chunk of the message payload
            ]
        ];

        byte[] expectedMessage = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

        // Act
        var messages = await TestMessageAssembly(chunks);

        // Assert
        messages.Should().HaveCount(1);
        messages[0].ToArray().Should().BeEquivalentTo(expectedMessage);
    }

    [TestCaseSource(nameof(GetChunkingTestCases))]
    public async Task CorrectlyAssemblesMessagesWithMultipleChunks(MessageChunkingTestCase testCase)
    {
        // Act
        var messages = await TestMessageAssembly(testCase.Chunks);

        // Assert
        messages.Should().HaveCount(testCase.ExpectedMessages.Length);
        for (var i = 0; i < testCase.ExpectedMessages.Length; i++)
        {
            messages[i].ToArray().Should().BeEquivalentTo(testCase.ExpectedMessages[i]);
        }
    }

    public class MessageChunkingTestCase
    {
        public required byte[][] Chunks { get; init; }
        public required byte[][] ExpectedMessages { get; init; }

        public override string ToString()
        {
            var messageSizes = ExpectedMessages.Select(x => $"{x.Length:000}");
            var messageLengthStr = string.Join(", ", messageSizes);

            var chunkSizes = Chunks.Select(x => $"{x.Length:000}");
            var chunkLengthStr = string.Join(", ", chunkSizes);

            return $"Message: [{messageLengthStr}], Chunks: [{chunkLengthStr}]";
        }
    }

    private static IEnumerable<MessageChunkingTestCase> GetChunkingTestCases()
    {
        const int numMessages = 2;
        const int maxMessageSize = 100;
        const int minMessageSize = 50;
        const int maxSplits = 1;

        List<byte[]> messages = [];
        for (var m = 0; m < numMessages; m++)
        {
            var messageSize = Random.Shared.Next(minMessageSize, maxMessageSize);
            var message = new byte[messageSize];
            Random.Shared.NextBytes(message);
            messages.Add(message);
        }

        var byteArrayBuilder = new ByteArrayBuilder();
        foreach (var message in messages)
        {
            byteArrayBuilder = byteArrayBuilder.PackStreamMessage(message);
        }

        // now we have a long byte array with all the messages packed in it
        var bytes = byteArrayBuilder.ToArray();
        var chunkSets = CreateChunkSets(bytes, maxSplits).ToArray();

        foreach (var chunkSet in chunkSets)
        {
            yield return new MessageChunkingTestCase()
            {
                ExpectedMessages = messages.ToArray(),
                Chunks = chunkSet
            };
        }
    }

    private static List<byte[][]> CreateChunkSets(Memory<byte> bytes, int maxDepth)
    {
        if (maxDepth == 0 || bytes.Length == 0)
        {
            return [[bytes.ToArray()]];
        }

        var chunkSets = new List<byte[][]>();
        for (var i = 0; i < bytes.Length; i++)
        {
            List<byte[]> chunkSet =
            [
                bytes.Span[..i].ToArray()
            ];

            var remainingChunks = CreateChunkSets(bytes[i..], maxDepth - 1);
            chunkSet.AddRange(remainingChunks.SelectMany(x => x));
            chunkSets.Add(chunkSet.ToArray());
        }

        return chunkSets;
    }

    private Task<ReadOnlySequence<byte>[]> TestMessageAssembly(IEnumerable<byte[]> chunks)
    {
        var chunkPipe = new TestChunkPipe(chunks);
        var assembler = new ChunkAssembler();

        var result = assembler.ReadMessagesAsync(chunkPipe.Reader).ToArrayAsync();
        chunkPipe.PlayMessages();
        return result.AsTask();
    }

    private class TestChunkPipe
    {
        private readonly IEnumerable<byte[]> _chunks;
        private readonly Pipe _pipe;

        public TestChunkPipe(IEnumerable<byte[]> chunks)
        {
            _chunks = chunks;
            _pipe = new Pipe();
        }

        public PipeReader Reader => _pipe.Reader;

        public void PlayMessages()
        {
            foreach (var chunk in _chunks)
            {
                _pipe.Writer.Write(chunk);
            }

            _pipe.Writer.Complete();
        }
    }
}
