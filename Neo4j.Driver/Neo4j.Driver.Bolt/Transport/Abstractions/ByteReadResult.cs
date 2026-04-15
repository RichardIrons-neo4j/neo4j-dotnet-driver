using System.Buffers;

namespace Neo4j.Driver.Bolt.Transport.Abstractions;

/// <summary>
/// Result of a read operation from an <see cref="IByteReader"/>.
/// </summary>
public readonly struct ByteReadResult
{
    /// <summary>
    /// The buffer containing the read data.
    /// </summary>
    public required ReadOnlySequence<byte> Buffer { get; init; }

    /// <summary>
    /// Whether the reader has completed (no more data will be available).
    /// </summary>
    public required bool IsCompleted { get; init; }
}