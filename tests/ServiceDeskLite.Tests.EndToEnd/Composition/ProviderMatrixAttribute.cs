using System.Reflection;
using Xunit.Sdk;

namespace ServiceDeskLite.Tests.EndToEnd.Composition;

/// <summary>
/// Supplies <see cref="PersistenceProvider"/> values as <c>[Theory]</c> inline data
/// so that every test runs once per provider.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ProviderMatrixAttribute : DataAttribute
{
    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        yield return [PersistenceProvider.InMemory];
        yield return [PersistenceProvider.Sqlite];
    }
}
