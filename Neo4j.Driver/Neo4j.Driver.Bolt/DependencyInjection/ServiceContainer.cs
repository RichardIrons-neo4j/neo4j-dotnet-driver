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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Neo4j.Driver.Bolt.Extensions;

namespace Neo4j.Driver.Bolt.DependencyInjection;

/// <summary>
/// Internal-use container: register implementations (several per service), instances, optional assembly scan,
/// and <see cref="Resolve{T}"/> with constructor injection. Several implementations for one service: plain
/// <c>Resolve&lt;T&gt;</c> uses the last registration; <c>IEnumerable&lt;T&gt;</c> returns all in order.
/// </summary>
public sealed class ServiceContainer : IServiceResolver
{
    private readonly Dictionary<Type, List<Type>> _implementationsByService = new();
    private readonly Dictionary<Type, object> _instances = new();

    /// <summary>
    /// Registers <typeparamref name="TImplementation"/> for <typeparamref name="TService"/>.
    /// Later registrations for the same service override plain <c>Resolve&lt;TService&gt;</c>; use
    /// <c>IEnumerable&lt;TService&gt;</c> for every implementation.
    /// </summary>
    public ServiceContainer Register<TService, TImplementation>()
        where TImplementation : class, TService
    {
        AddRegistration(typeof(TService), typeof(TImplementation));
        return this;
    }

    public ServiceContainer RegisterInstance<TService>(TService instance)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        _instances[typeof(TService)] = instance;
        return this;
    }

    /// <summary>
    /// Registers concrete types in <paramref name="assembly"/> as themselves and for each interface
    /// also defined in that assembly. Types ordered by full name.
    /// </summary>
    public ServiceContainer RegisterTypesFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        foreach (var type in GetLoadableTypes(assembly).OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters)
            {
                continue;
            }

            if (type.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length > 0)
            {
                continue;
            }

            AddRegistration(type, type);

            foreach (var iface in type.GetInterfaces())
            {
                if (iface.Assembly == assembly)
                {
                    AddRegistration(iface, type);
                }
            }
        }

        return this;
    }

    public ServiceContainer RegisterTypesFromThisAssembly() =>
        RegisterTypesFromAssembly(typeof(ServiceContainer).Assembly);

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

        if (IsIEnumerableOfT(serviceType))
        {
            return ResolveAll(serviceType, resolutionStack);
        }

        var implementations = GetRegisteredImplementationTypes(serviceType);
        InvalidOperationException.ThrowIf(
            implementations.Count == 0,
            () => new InvalidOperationException($"No registration for {serviceType.FullName}."));

        return Activate(implementations[^1], resolutionStack);
    }

    private static bool IsIEnumerableOfT(Type serviceType) =>
        serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>);

    private object ResolveAll(Type serviceType, Stack<Type> resolutionStack)
    {
        var elementType = serviceType.GetGenericArguments()[0];
        var implTypes = GetRegisteredImplementationTypes(elementType);
        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;
        
        foreach (var impl in implTypes)
        {
            list.Add(Activate(impl, resolutionStack));
        }

        return list;
    }

    private object Activate(Type concreteType, Stack<Type> resolutionStack)
    {
        if (resolutionStack.Contains(concreteType))
        {
            var path = string.Join(
                " -> ",
                resolutionStack.Reverse().Append(concreteType).Select(static t => t.FullName ?? t.Name));
            throw new InvalidOperationException(
                $"Circular dependency while resolving {concreteType.FullName}. Path: {path}.");
        }

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

            return Activator.CreateInstance(concreteType, args)
                ?? throw new InvalidOperationException(
                    $"Failed to create instance of {concreteType.FullName}.");
        }
        finally
        {
            resolutionStack.Pop();
        }
    }

    private IReadOnlyList<Type> GetRegisteredImplementationTypes(Type serviceType)
    {
        if (_implementationsByService.TryGetValue(serviceType, out var list) && list.Count > 0)
        {
            return list;
        }

        if (serviceType is { IsClass: true, IsAbstract: false })
        {
            return [serviceType];
        }

        return [];
    }

    private void AddRegistration(Type serviceType, Type implementationType)
    {
        if (!_implementationsByService.TryGetValue(serviceType, out var list))
        {
            list = [];
            _implementationsByService[serviceType] = list;
        }

        if (!list.Contains(implementationType))
        {
            list.Add(implementationType);
        }
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
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
