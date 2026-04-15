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

using Microsoft.Extensions.Logging;

namespace Neo4j.Driver.Bolt.Tests.TestHelpers;

public class ConsoleLogger : ILogger
{
    private readonly LogLevel _logLevel;

    public ConsoleLogger(LogLevel logLevel = LogLevel.Trace)
    {
        _logLevel = logLevel;
    }
    
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if(logLevel < _logLevel)
        {
            return;
        }

        var message = formatter(state, exception);
        var level = logLevel.ToString().ToUpper();
        var header = $"[{level}]" + new string(' ', 7 - level.Length);
        Console.WriteLine($"{header}{message}");
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= _logLevel;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }
}
