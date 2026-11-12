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

#nullable enable

using System;
using System.Runtime.Serialization;

namespace Neo4j.Driver;

/// <summary>
/// An <see cref="IncompleteCommitException"/> indicates that the connection to the server was lost while a
/// transaction was being committed, so it is unknown whether the transaction was committed. It is not retried.
/// </summary>
[DataContract]
public class IncompleteCommitException : ServiceUnavailableException
{
    /// <summary>Create a new <see cref="IncompleteCommitException"/> with an error message.</summary>
    /// <param name="message">The error message.</param>
    public IncompleteCommitException(string message) : base(message)
    {
    }

    /// <summary>Create a new <see cref="IncompleteCommitException"/> with an error message and an exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public IncompleteCommitException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <inheritdoc/>
    public override bool IsRetriable
    {
        get { return false; }
    }
}
