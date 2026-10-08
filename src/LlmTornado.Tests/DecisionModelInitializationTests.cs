using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using LlmTornado.Decision.Models;

namespace LlmTornado.Tests;

[TestFixture]
public class DecisionModelInitializationTests
{
    [TestCase("DecisionModel", "OpenRouter")]
    [TestCase("DecisionModel", ".ctor")]
    [TestCase("OpenRouter.DecisionModelOpenRouter", "ModelsAll")]
    [TestCase("OpenRouter.DecisionModelOpenRouterAll", "ModelsAll")]
    [TestCase("OpenRouter.DecisionModelOpenRouterAll", "ModelGpt6LunaDecisions")]
    [TestCase("TypeSafe.DecisionModelTypeSafe", "ModelsAll")]
    [TestCase("TypeSafe.DecisionModelTypeSafeJev", "ModelsAll")]
    public void FirstAccess_InitializesCompleteCatalog(string typeName, string memberName)
    {
        // Test discovery can initialize the default assembly before any test runs.
        // A separate load context gives each entry point fresh static state.
        AssemblyLoadContext context = new AssemblyLoadContext(Guid.NewGuid().ToString(), isCollectible: true);
        try
        {
            Assembly assembly = context.LoadFromAssemblyPath(typeof(DecisionModel).Assembly.Location);
            Assert.That(assembly, Is.Not.SameAs(typeof(DecisionModel).Assembly));
            Type entry = assembly.GetType($"LlmTornado.Decision.Models.{typeName}", throwOnError: true)!;
            object? first = memberName == ".ctor"
                ? Activator.CreateInstance(entry, new object?[] { "openai/gpt-6-luna-decisions", null })
                : ReadMember(entry, memberName);
            Assert.That(first, Is.Not.Null);
            AssertCatalog(assembly);
        }
        finally
        {
            context.Unload();
        }
    }

    [Test]
    public async Task ConcurrentFirstAccess_InitializesCompleteCatalog()
    {
        AssemblyLoadContext context = new AssemblyLoadContext(Guid.NewGuid().ToString(), isCollectible: true);
        try
        {
            Assembly assembly = context.LoadFromAssemblyPath(typeof(DecisionModel).Assembly.Location);
            using ManualResetEventSlim start = new ManualResetEventSlim();
            var entries = new[]
            {
                ("DecisionModel", "AllModelsMap"),
                ("OpenRouter.DecisionModelOpenRouter", "ModelsAll"),
                ("OpenRouter.DecisionModelOpenRouterAll", "ModelGpt6LunaDecisions"),
                ("TypeSafe.DecisionModelTypeSafe", "ModelsAll"),
                ("TypeSafe.DecisionModelTypeSafeJev", "ModelLatest")
            };
            Task[] reads = entries.Select(entry => Task.Run(() =>
            {
                start.Wait();
                Type type = assembly.GetType($"LlmTornado.Decision.Models.{entry.Item1}", throwOnError: true)!;
                Assert.That(ReadMember(type, entry.Item2), Is.Not.Null);
            })).ToArray();
            start.Set();
            await Task.WhenAll(reads).WaitAsync(TimeSpan.FromSeconds(15));
            AssertCatalog(assembly);
        }
        finally
        {
            context.Unload();
        }
    }

    private static void AssertCatalog(Assembly assembly)
    {
        Type root = assembly.GetType("LlmTornado.Decision.Models.DecisionModel", throwOnError: true)!;
        foreach (string name in new[] { "TypeSafe", "OpenRouter", "AllModels", "AllModelsMap" })
        {
            Assert.That(root.GetField(name, BindingFlags.Public | BindingFlags.Static)?.IsInitOnly, Is.True, name);
        }
        IList models = (IList)ReadMember(root, "AllModels");
        IDictionary map = (IDictionary)ReadMember(root, "AllModelsMap");
        Assert.That(models, Has.Count.EqualTo(16));
        Assert.That(map, Has.Count.EqualTo(models.Count));
        Assert.That(ReadMember(root, "AllModels"), Is.SameAs(models));
        Assert.That(ReadMember(root, "AllModelsMap"), Is.SameAs(map));

        foreach (object model in models)
        {
            string name = (string)ReadMember(model.GetType(), "Name", model);
            object provider = ReadMember(model.GetType(), "Provider", model);
            Assert.That(map[name], Is.SameAs(model), name);
            Assert.That(root.GetMethod("GetProvider")!.Invoke(null, new object[] { name }), Is.EqualTo(provider), name);
            object inferred = root.GetMethod("op_Implicit")!.Invoke(null, new object[] { name })!;
            Assert.That(ReadMember(root, "Provider", inferred), Is.EqualTo(provider), name);
        }

        foreach (var (providerName, catalogName, count) in new[] { ("OpenRouter", "All", 13), ("TypeSafe", "Jev", 3) })
        {
            object provider = ReadMember(root, providerName);
            IList vendorModels = (IList)ReadMember(provider.GetType(), "AllModels", provider);
            Assert.That(vendorModels, Has.Count.EqualTo(count));
            object catalog = ReadMember(provider.GetType(), catalogName, provider);
            FieldInfo[] aliases = catalog.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(field => field.FieldType == root).ToArray();
            Assert.That(aliases, Has.Length.EqualTo(count));
            foreach (FieldInfo alias in aliases)
            {
                object? model = alias.GetValue(catalog);
                Assert.That(model, Is.Not.Null, alias.Name);
                string name = (string)ReadMember(root, "Name", model);
                Assert.That(map[name], Is.SameAs(model), name);
                Assert.That(ReadMember(catalog.GetType(), "Model" + alias.Name), Is.SameAs(model), alias.Name);
                Assert.That(vendorModels.Contains(model), Is.True, name);
                MethodInfo owns = provider.GetType().GetMethod("OwnsModel", new[] { typeof(string) })!;
                Assert.That(owns.Invoke(provider, new object[] { name }), Is.True, name);
            }
        }

        Assert.That(root.GetMethod("GetProvider")!.Invoke(null, new object[] { "unknown-model" }), Is.Null);
        object custom = Activator.CreateInstance(root, new object?[] { "unknown-model", null })!;
        Assert.That(ReadMember(root, "Provider", custom).ToString(), Is.EqualTo("TypeSafe"));
    }

    private static object ReadMember(Type type, string name, object? instance = null)
    {
        BindingFlags flags = BindingFlags.Public | (instance is null ? BindingFlags.Static : BindingFlags.Instance);
        return type.GetField(name, flags)?.GetValue(instance)
            ?? type.GetProperty(name, flags)?.GetValue(instance)
            ?? throw new InvalidOperationException($"{type.FullName}.{name} is missing or null.");
    }
}
