// Copyright (c) "Neo4j"
// Neo4j Sweden AB [http://neo4j.com]
// 
// This file is part of Neo4j.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Neo4j.Driver.Tests;

public class TransactionConfigTests
{
    public class TimeoutField
    {
        public static IEnumerable<object[]> InvalidTimeSpanValues => new[]
        {
            new object[] { TimeSpan.FromSeconds(-1) },
            new object[] { TimeSpan.FromHours(-2) }
        };

        public static IEnumerable<object[]> ValidTimeSpanValues => new[]
        {
            new object[] { null },
            new object[] { (TimeSpan?)TimeSpan.Zero },
            new object[] { (TimeSpan?)TimeSpan.FromMilliseconds(0.1) },
            new object[] { (TimeSpan?)TimeSpan.FromMinutes(30) },
            new object[] { (TimeSpan?)TimeSpan.MaxValue }
        };

        [Fact]
        public void ShouldReturnDefaultValueAsNull()
        {
            var config = new TransactionConfig();

            config.Timeout.Should().Be(null);
        }

        [Theory]
        [MemberData(nameof(ValidTimeSpanValues))]
        public void ShouldAllowToSetToNewValue(TimeSpan? input)
        {
            var builder = TransactionConfig.Builder;
            builder.WithTimeout(input);

            var config = builder.Build();

            config.Timeout.Should().Be(input);
        }

        [Theory]
        [MemberData(nameof(InvalidTimeSpanValues))]
        public void ShouldThrowExceptionIfAssigningValueLessThanZero(TimeSpan input)
        {
            var error = Record.Exception(() => TransactionConfig.Builder.WithTimeout(input));

            error.Should().BeOfType<ArgumentOutOfRangeException>();
            error.Message.Should().Contain("not be negative");
        }
    }

    public class MetadataField
    {
        [Fact]
        public void ShouldReturnDefaultValueEmptyDictionary()
        {
            var config = new TransactionConfig();

            config.Metadata.Should().BeEmpty();
        }

        [Fact]
        public void ShouldAllowToSetToNewValue()
        {
            var builder = TransactionConfig.Builder;
            builder.WithMetadata(new Dictionary<string, object> { { "key", "value" } });

            var config = builder.Build();

            config.Metadata.Should()
                .HaveCount(1)
                .And.Contain(new KeyValuePair<string, object>("key", "value"));
        }

        [Fact]
        public void ShouldThrowExceptionIfAssigningNull()
        {
            var error = Record.Exception(() => TransactionConfig.Builder.WithMetadata(null));

            error.Should().BeOfType<ArgumentNullException>();
            error.Message.Should().Contain("should not be null");
        }
    }
}
