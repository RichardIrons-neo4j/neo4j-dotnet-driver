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
/// When more than one implementation is registered for a service, use
/// <c>Resolve&lt;IEnumerable&lt;T&gt;&gt;()</c> or <c>Resolve&lt;T[]&gt;()</c> (or <c>IReadOnlyList&lt;T&gt;</c>, etc.).
/// </summary>
public interface IServiceResolver
{
    /// <summary>
    /// Resolves <typeparamref name="T"/> (interface, abstract base, or concrete type).
    /// Throws if multiple implementations are registered for the same service type.
    /// </summary>
    T Resolve<T>()
        where T : notnull;

    /// <summary>
    /// Resolves the given service type. For <c>IEnumerable&lt;T&gt;</c>, <c>T[]</c>, <c>IReadOnlyList&lt;T&gt;</c>,
    /// <c>ICollection&lt;T&gt;</c>, and <c>IList&lt;T&gt;</c>, returns all registered implementations of <c>T</c>
    /// in registration order.
    /// </summary>
    object Resolve(Type serviceType);
}
