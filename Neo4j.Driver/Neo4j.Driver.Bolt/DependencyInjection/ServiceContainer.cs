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
using System.Linq;
using System.Reflection;
using Neo4j.Driver.Bolt.Extensions;

namespace Neo4j.Driver.Bolt.DependencyInjection;

/// <summary>
/// Minimal container: map abstractions to implementations, optional pre-built instances,
/// and <see cref="Resolve{T}"/> with recursive public-constructor injection (transient).
/// Circular dependencies are detected by tracking the in-progress resolution stack of concrete types
/// (push before resolving constructor parameters, pop in <c>finally</c>).
/// </summary>
public sealed class ServiceContainer : IServiceResolver
{
    private readonly Dictionary<Type, Type> _implementationByService = new();
    private readonly Dictionary<Type, object> _instances = new();

    /// <summary>
    /// Maps <typeparamref name="TService"/> to <typeparamref name="TImplementation"/>.
    /// </summary>
    public ServiceContainer Register<TService, TImplementation>()
        where TImplementation : class, TService
    {
        _implementationByService[typeof(TService)] = typeof(TImplementation);
        return this;
    }

    /// <summary>
    /// Registers a pre-built instance . Always returned as-is from <see cref="Resolve"/>.
    /// </summary>
    public ServiceContainer RegisterInstance<TService>(TService instance)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        _instances[typeof(TService)] = instance;
        return this;
    }

    /// <inheritdoc />
    public T Resolve<T>()
        where T : notnull =>
        (T)Resolve(typeof(T));

    /// <inheritdoc />
    public object Resolve(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return ResolveCore(serviceType, new Stack<Type>());
    }

    private object ResolveCore(Type serviceType, Stack<Type> resolutionStack)
    {
        if (_instances.TryGetValue(serviceType, out var instance))
        {
            return instance;
        }

        var concreteType = ResolveConcreteType(serviceType);

        InvalidOperationException.ThrowIf(resolutionStack.Contains(concreteType),
            $"Circular dependency while resolving {concreteType.FullName}.");

        resolutionStack.Push(concreteType);
        try
        {
            var constructor = SelectConstructor(concreteType);
            var parameters = constructor.GetParameters();
            var args = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                args[i] = ResolveCore(parameters[i].ParameterType, resolutionStack);
            }

            var created = Activator.CreateInstance(concreteType, args)
                ?? throw new InvalidOperationException(
                    $"Failed to create instance of {concreteType.FullName}.");

            return created;
        }
        finally
        {
            resolutionStack.Pop();
        }
    }

    private Type ResolveConcreteType(Type requested)
    {
        if (_implementationByService.TryGetValue(requested, out var mapped))
        {
            return mapped;
        }

        return requested is { IsClass: true, IsAbstract: false } 
            ? requested : 
            throw new InvalidOperationException($"No registration for {requested.FullName}.");
    }

    private static ConstructorInfo SelectConstructor(Type concreteType)
    {
        var constructors = concreteType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);

        if (constructors.Length == 1)
        {
            return constructors[0];
        }

        return constructors.MaxBy(c => c.GetParameters().Length)
            ?? throw new InvalidOperationException($"{concreteType.FullName} has no public constructors.");
    }
}
