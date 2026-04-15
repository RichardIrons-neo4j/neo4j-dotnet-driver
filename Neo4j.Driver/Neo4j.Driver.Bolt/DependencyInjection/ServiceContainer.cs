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
/// Minimal container: map abstractions to implementations (multiple per service allowed),
/// optional pre-built instances, and <see cref="Resolve{T}"/> with recursive constructor injection (transient).
/// Use <see cref="RegisterTypesFromAssembly"/> to register all public and internal concrete types against
/// interfaces defined in the same assembly. Resolve <c>IEnumerable&lt;T&gt;</c> or <c>T[]</c> to obtain all
/// implementations of <c>T</c>. Circular dependencies are detected via a <see cref="Stack{T}"/> of concrete types.
/// </summary>
public sealed class ServiceContainer : IServiceResolver
{
    private readonly Dictionary<Type, List<Type>> _implementationsByService = new();
    private readonly Dictionary<Type, object> _instances = new();

    /// <summary>
    /// Adds <typeparamref name="TImplementation"/> as an implementation of <typeparamref name="TService"/>.
    /// Multiple implementations may be registered for the same service; use
    /// <c>Resolve&lt;IEnumerable&lt;TService&gt;&gt;()</c> or <c>Resolve&lt;TService[]&gt;()</c> to retrieve them all.
    /// </summary>
    public ServiceContainer Register<TService, TImplementation>()
        where TImplementation : class, TService
    {
        AddRegistration(typeof(TService), typeof(TImplementation));
        return this;
    }

    /// <summary>
    /// Registers a pre-built instance. Looked up before implementation lists.
    /// </summary>
    public ServiceContainer RegisterInstance<TService>(TService instance)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(instance);
        _instances[typeof(TService)] = instance;
        return this;
    }

    /// <summary>
    /// Scans <paramref name="assembly"/> for non-abstract classes and registers each against every interface
    /// that interface is defined in <paramref name="assembly"/> (same assembly as the implementation).
    /// Also registers each concrete type as its own service if it is not generic.
    /// Types are processed in stable order by full name.
    /// </summary>
    /// <param name="assembly">Assembly to scan (e.g. <see cref="Assembly.GetExecutingAssembly"/> or <c>typeof(T).Assembly</c>).</param>
    /// <param name="implementationFilter">Optional; return <c>false</c> to skip a concrete type entirely.</param>
    /// <param name="interfaceFilter">Optional; return <c>false</c> to skip registering a (implementation, interface) pair.</param>
    public ServiceContainer RegisterTypesFromAssembly(
        Assembly assembly,
        Func<Type, bool>? implementationFilter = null,
        Func<Type, bool>? interfaceFilter = null)
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

            if (implementationFilter?.Invoke(type) == false)
            {
                continue;
            }

            AddRegistration(type, type);

            foreach (var iface in type.GetInterfaces())
            {
                if (iface.Assembly != assembly)
                {
                    continue;
                }

                if (interfaceFilter?.Invoke(iface) == false)
                {
                    continue;
                }

                AddRegistration(iface, type);
            }
        }

        return this;
    }

    /// <summary>
    /// Registers types from the assembly that contains <see cref="ServiceContainer"/>.
    /// </summary>
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

        if (TryResolveEnumerable(serviceType, resolutionStack, out var enumerableResult))
        {
            return enumerableResult;
        }

        var implementations = GetRegisteredImplementationTypes(serviceType);
        InvalidOperationException.ThrowIf(
            implementations.Count == 0,
            () => new InvalidOperationException($"No registration for {serviceType.FullName}."));

        InvalidOperationException.ThrowIf(
            implementations.Count > 1,
            () => new InvalidOperationException(
                $"Multiple implementations ({implementations.Count}) are registered for {serviceType.FullName}. " +
                $"Resolve IEnumerable<{serviceType.Name}> or {serviceType.Name}[] to obtain all implementations."));

        return Activate(implementations[0], resolutionStack);
    }

    private bool TryResolveEnumerable(Type serviceType, Stack<Type> resolutionStack, out object result)
    {
        Type? elementType = null;
        var asArray = false;

        if (serviceType.IsArray && serviceType.GetArrayRank() == 1)
        {
            elementType = serviceType.GetElementType();
            asArray = true;
        }
        else if (serviceType.IsGenericType)
        {
            var def = serviceType.GetGenericTypeDefinition();
            if (def == typeof(IEnumerable<>) ||
                def == typeof(IReadOnlyList<>) ||
                def == typeof(IReadOnlyCollection<>) ||
                def == typeof(IList<>) ||
                def == typeof(ICollection<>))
            {
                elementType = serviceType.GetGenericArguments()[0];
            }
        }

        if (elementType is null)
        {
            result = null!;
            return false;
        }

        var implTypes = GetRegisteredImplementationTypes(elementType);
        var items = new List<object>(implTypes.Count);
        foreach (var impl in implTypes)
        {
            items.Add(Activate(impl, resolutionStack));
        }

        if (asArray)
        {
            var array = Array.CreateInstance(elementType, items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                array.SetValue(items[i], i);
            }

            result = array;
            return true;
        }

        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var item in items)
        {
            list.Add(item);
        }

        result = list;
        return true;
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
