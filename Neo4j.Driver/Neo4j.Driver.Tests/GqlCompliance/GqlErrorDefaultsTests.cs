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

using System.Collections.Generic;
using FluentAssertions;
using Neo4j.Driver.Internal.GqlCompliance;
using Neo4j.Driver.Internal.Messaging;
using Xunit;

namespace Neo4j.Driver.Tests.GqlCompliance;

public class GqlErrorDefaultsTests
{
    [Fact]
    public void Fill_ShouldAddMissingDiagnosticRecordEntries()
    {
        var message = new FailureMessage("Neo.ClientError.Statement.SyntaxError", "Invalid input")
        {
            GqlDiagnosticRecord = new Dictionary<string, object> { { "key", "value" } }
        };

        new GqlErrorDefaults().Fill(message);

        message.GqlDiagnosticRecord.Should().BeEquivalentTo(
            new Dictionary<string, object>
            {
                ["key"] = "value",
                ["OPERATION"] = "",
                ["OPERATION_CODE"] = "0",
                ["CURRENT_SCHEMA"] = "/"
            });
    }

    [Fact]
    public void Fill_ShouldClassifyAsUnknown_WhenTheDiagnosticRecordHasNoClassification()
    {
        var message = new FailureMessage("Neo.ClientError.Statement.SyntaxError", "Invalid input");

        new GqlErrorDefaults().Fill(message);

        message.GqlRawClassification.Should().BeNull();
        message.GqlClassification.Should().Be("UNKNOWN");
    }
}
