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

namespace Neo4j.Driver.Bolt.DependencyInjection;

/// <summary>
/// Resolves registered services with recursive constructor injection.
/// Multiple implementations: resolve <c>IEnumerable&lt;T&gt;</c>.
/// </summary>
public interface IServiceResolver
{
    /// <summary>
    /// Resolves <typeparamref name="T"/>. Throws if several implementations are registered for the same service.
    /// </summary>
    T Resolve<T>()
        where T : notnull;

    /// <summary>
    /// Resolves the service type. <c>IEnumerable&lt;T&gt;</c> yields all implementations of <c>T</c> in registration order.
    /// </summary>
    object Resolve(Type serviceType);
}
