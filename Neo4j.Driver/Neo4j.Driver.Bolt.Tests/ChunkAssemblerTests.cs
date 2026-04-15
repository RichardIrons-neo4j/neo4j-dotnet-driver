using System.Buffers;
using System.IO.Pipelines;
using FluentAssertions;
using Neo4j.Driver.Bolt.Tests.TestHelpers;
using Neo4j.Driver.Bolt.Transport.Implementations;
using NUnit.Framework;

namespace Neo4j.Driver.Bolt.Tests;

[TestFixture]
public class ChunkAssemblerTests : UnitTestBase<ChunkAssembler>
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
        byte[] expectedBytes = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

        // Act
        var messages = await Subject.ReadMessagesAsync(reader).ToListAsync();

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
        var messageResults = messages.ToArray();
        messageResults.Should().HaveCount(1);
        messageResults[0].ToArray().Should().BeEquivalentTo(expectedMessage);
    }

    [Test]
    public async Task ThrowsOnIncompleteHeader()
    {
        byte[] bytes = [0x00]; // Only 1 byte of header
        var reader = PipeReader.Create(new ReadOnlySequence<byte>(bytes));
        
        var act = async () =>  await Subject.ReadMessagesAsync(reader).ToListAsync();

        await act.Should().ThrowAsync<ProtocolException>();
    }
    
    [Test]
    public async Task ThrowsOnIncompleteBody()
    {
        // Header says 10 bytes, only 2 provided
        byte[] bytes = [0x00, 0x0A, 0x01, 0x02];
        var reader = PipeReader.Create(new ReadOnlySequence<byte>(bytes));
    
        var act = async () => await Subject.ReadMessagesAsync(reader).ToListAsync();
    
        await act.Should().ThrowAsync<ProtocolException>();
    }
    
    [Test]
    public async Task HandlesZeroLengthMessage()
    {
        byte[] bytes = [0x00, 0x00]; // Message size = 0
        var reader = PipeReader.Create(new ReadOnlySequence<byte>(bytes));
    
        var messages = await Subject.ReadMessagesAsync(reader).ToListAsync();
    
        messages.Should().HaveCount(1);
        messages[0].Length.Should().Be(0);
    }
    
    [Test]
    public async Task HandlesEmptyPipe()
    {
        var reader = PipeReader.Create(new ReadOnlySequence<byte>([]));
    
        var messages = await Subject.ReadMessagesAsync(reader).ToListAsync();
    
        messages.Should().BeEmpty();
    }
    
    [Test]
    public async Task ThrowsOnCancellation()
    {
        var pipe = new Pipe();
        using var cts = new CancellationTokenSource();
    
        var readTask = Subject.ReadMessagesAsync(pipe.Reader, cts.Token).ToListAsync();
    
        // Cancel before any data arrives
        cts.Cancel();
    
        var act = async () => await readTask;
    
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
    
    [Test]
    public async Task ThrowsOnIncompleteSecondMessage()
    {
        // First message complete, second message incomplete
        byte[] bytes =
        [
            0x00, 0x02, 0xAA, 0xBB, // Complete message (2 bytes)
            0x00, 0x05, 0x01        // Incomplete message (header says 5, only 1 provided)
        ];
        var reader = PipeReader.Create(new ReadOnlySequence<byte>(bytes));
    
        var act = async () => await Subject.ReadMessagesAsync(reader).ToListAsync();
    
        await act.Should().ThrowAsync<ProtocolException>();
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

    private static List<MessageChunkingTestCase> GetChunkingTestCases()
    {
        const int numMessages = 5;
        const int minMessageSize = 0;
        const int maxMessageSize = 5;
        const int maxSplits = 3;
        const int minChunkSize = 5;

        List<byte[]> messages = [];
        var byteArrayBuilder = new ByteArrayBuilder();
        for (var m = 0; m < numMessages; m++)
        {
            var messageSize = Random.Shared.Next(minMessageSize, maxMessageSize + 1);
            var message = new byte[messageSize];
            Random.Shared.NextBytes(message);
            messages.Add(message);
            byteArrayBuilder = byteArrayBuilder.PackStreamMessage(message);
        }

        // now we have a long byte array with all the messages packed in it
        var bytes = byteArrayBuilder.ToArray();
        var chunkSets = CreateChunkSets(bytes, maxSplits, minChunkSize).ToArray();

        return chunkSets
            .Select(chunkSet => new MessageChunkingTestCase()
            {
                ExpectedMessages = messages.ToArray(),
                Chunks = chunkSet
            })
            .ToList();
    }

    public class MessageChunkingTestCase
    {
        public required byte[][] Chunks { get; init; }
        public required byte[][] ExpectedMessages { get; init; }

        public override string ToString()
        {
            var messageSizes = ExpectedMessages.Select(x => $"{x.Length:000}");
            var messageLengthStr = string.Join(", ", messageSizes);
            var totalMessageLength = ExpectedMessages.Sum(x => x.Length) + ExpectedMessages.Length * sizeof(short);

            var chunkSizes = Chunks.Select(x => $"{x.Length:000}");
            var chunkLengthStr = string.Join(", ", chunkSizes);
            var totalChunkLength = Chunks.Sum(x => x.Length);

            return
                $"Message lengths: [{messageLengthStr}] ({totalMessageLength}), " +
                $"Chunk lengths: [{chunkLengthStr}] ({totalChunkLength})";
        }
    }

    private static List<byte[][]> CreateChunkSets(Memory<byte> bytes, int parts, int minSize = 1)
    {
        var result = new List<byte[][]>();
        var sizes = SetSizeGenerator.GenerateSizes(bytes.Length, parts, minSize);
        foreach (var size in sizes)
        {
            var chunkSet = new List<byte[]>();
            var offset = 0;
            foreach (var s in size)
            {
                chunkSet.Add(bytes.Slice(offset, s).ToArray());
                offset += s;
            }
            result.Add(chunkSet.ToArray());
        }

        return result;
    }

    private async Task<byte[][]> TestMessageAssembly(IEnumerable<byte[]> chunks)
    {
        var chunkPipe = new TestChunkPipe(chunks);
        var messages = new List<byte[]>();
        
        var result = Task.Run(async () =>
        {
            await foreach (var readMessage in Subject.ReadMessagesAsync(chunkPipe.Reader).ConfigureAwait(false))
            {
                messages.Add(readMessage.ToArray());
            }
            
            return messages.ToArray();
        });
        
        await chunkPipe.PlayMessages().ConfigureAwait(false);
        return await result.ConfigureAwait(false);
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

        public async Task PlayMessages()
        {
            foreach (var chunk in _chunks)
            {
                await _pipe.Writer.WriteAsync(chunk).ConfigureAwait(false);
            }

            await _pipe.Writer.CompleteAsync().ConfigureAwait(false);
        }
    }
}
