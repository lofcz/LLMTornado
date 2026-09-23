using System.Globalization;
using System.Text;
using LlmTornado.Code;
using LlmTornado.Models;

namespace LlmTornado.Internal.ProviderGenerator;

class Program
{
    record ProviderConfig(
        LLmProviders Provider,
        string NamespaceName,
        string ClassName,
        Func<string, string> NormalizeFunction,
        string Description
    );

    static async Task Main(string[] args)
    {
        List<ProviderConfig> providers =
        [
            new ProviderConfig(
                Provider: LLmProviders.OpenRouter,
                NamespaceName: "OpenRouter",
                ClassName: "ChatModelOpenRouterAll",
                NormalizeFunction: NormalizeOpenRouter,
                Description: "All models from Open Router."
            ),

            new ProviderConfig(
                Provider: LLmProviders.Requesty,
                NamespaceName: "Requesty",
                ClassName: "ChatModelRequestyAll",
                NormalizeFunction: NormalizeRequesty,
                Description: "All models from Requesty."
            ),

            new ProviderConfig(
                Provider: LLmProviders.Opper,
                NamespaceName: "Opper",
                ClassName: "ChatModelOpperAll",
                NormalizeFunction: NormalizeOpper,
                Description: "All models from Opper."
            )
        ];
        
        foreach (ProviderConfig config in providers)
        {
            await GenerateCodeForProvider(config);
        }
    }

    static async Task GenerateCodeForProvider(ProviderConfig config)
    {
        List<RetrievedModel>? models = await new TornadoApi(config.Provider).Models.GetModels(config.Provider);
        models = models?.OrderBy(x => x.Id, StringComparer.Ordinal).ToList();

        if (models is null || models.Count == 0)
        {
            Console.WriteLine($"No models found for provider {config.Provider}");
            return;
        }

        StringBuilder sb = new StringBuilder();
        HashSet<string> usedIdents = [];
        List<RetrievedModel> skip = [];

        AppendLine("// This code was generated with LlmTornado.Internal.ProviderGenerator");
        AppendLine("// do not edit manually");
        AppendLine();
        AppendLine("using System;");
        AppendLine("using System.Collections.Generic;");
        AppendLine("using LlmTornado.Code.Models;");
        AppendLine("using LlmTornado.Code;");
        AppendLine();
        AppendLine($"namespace LlmTornado.Chat.Models.{config.NamespaceName};");
        AppendLine();
        AppendLine($"/// <summary>");
        AppendLine($"/// {config.Description}");
        AppendLine($"/// </summary>");
        AppendLine($"public class {config.ClassName} : IVendorModelClassProvider");
        AppendLine("{");

        int i = 0;
        List<string> nonSkippedModelIdentifiers = [];

        foreach (RetrievedModel model in models)
        {
            string identifier = config.NormalizeFunction(model.Id);

            if (!usedIdents.Add(identifier))
            {
                skip.Add(model);
                continue;
            }

            nonSkippedModelIdentifiers.Add($"Model{identifier}");

            AppendLine($"/// <summary>", 1);
            AppendLine($"/// {model.Id}", 1);
            AppendLine($"/// </summary>", 1);
            AppendLine($"public static readonly ChatModel Model{identifier} = new ChatModel(\"{model.Id}\", \"{model.Id}\", LLmProviders.{config.Provider}, {model.ContextLength});", 1);
            AppendLine();

            AppendLine($"/// <summary>", 1);
            AppendLine($"/// <inheritdoc cref=\"Model{identifier}\"/>", 1);
            AppendLine($"/// </summary>", 1);
            AppendLine($"public readonly ChatModel {identifier} = Model{identifier};", 1);

            if (i < models.Count - 1)
            {
                AppendLine();
            }

            i++;
        }

        AppendLine();

        AppendLine($"/// <summary>", 1);
        AppendLine($"/// All known models from {config.NamespaceName}.", 1);
        AppendLine($"/// </summary>", 1);
        AppendLine("public static List<IModel> ModelsAll => LazyModelsAll.Value;", 1);
        AppendLine();

        AppendLine($"private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [{string.Join(", ", nonSkippedModelIdentifiers)}]);", 1);
        AppendLine();

        AppendLine($"/// <summary>", 1);
        AppendLine($"/// <inheritdoc cref=\"ModelsAll\"/>", 1);
        AppendLine($"/// </summary>", 1);
        AppendLine("public List<IModel> AllModels => ModelsAll;", 1);
        AppendLine();

        AppendLine($"internal {config.ClassName}()", 1);
        AppendLine("{", 1);
        AppendLine();
        AppendLine("}", 1);

        AppendLine("}");

        string code = sb.ToString().Trim();

        string assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        string combined = Path.Combine(assemblyPath, "..", "..", "..", "..", "LlmTornado", "Chat", "Models", config.NamespaceName, $"{config.ClassName}.cs");
        string abs = Path.GetFullPath(combined);

        if (File.Exists(abs))
        {
            await File.WriteAllTextAsync(abs, code);
            Console.WriteLine($"Generated code for {config.Provider} -> {abs}");
        }
        else
        {
            Console.WriteLine($"Target file does not exist: {abs}");
        }

        void AppendLine(string content = "", int identLevel = 0)
        {
            if (content.Length is 0)
            {
                sb.AppendLine();
                return;
            }

            if (identLevel > 0)
            {
                sb.Append(new string(' ', identLevel * 4));
            }

            sb.AppendLine(content);
        }
    }
    
    static string NormalizeOpenRouter(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        int lastSlashIndex = input.LastIndexOf('/');
        string relevantPart = lastSlashIndex > -1 ? input[(lastSlashIndex + 1)..] : input;
        string withoutDots = relevantPart.Replace(".", string.Empty);
        char[] delimiters = ['-', '_', ':'];
        string[] segments = withoutDots.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
        StringBuilder resultBuilder = new StringBuilder();

        foreach (string segment in segments)
        {
            if (string.IsNullOrEmpty(segment))
            {
                continue;
            }

            resultBuilder.Append(char.ToUpperInvariant(segment[0]));

            if (segment.Length > 1)
            {
                resultBuilder.Append(segment.AsSpan(1));
            }
        }

        return resultBuilder.ToString();
    }
    
    static string NormalizeOpper(string input)
    {
        // Opper ids are bare pool names such as "claude-sonnet-4-6". A pinned route
        // carries a "provider/" prefix, e.g. "azure/gpt-5.5"; keep the model part.
        int slash = input.LastIndexOf('/');
        return slash >= 0 ? input[(slash + 1)..] : input;
    }

    static string NormalizeRequesty(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        string normalized = input.Replace('/', '_').Replace('-', '_').Replace('.', '_').Replace(':', '_').Replace('@', '_');
        TextInfo textInfo = CultureInfo.CurrentCulture.TextInfo;
        string titleCaseString = textInfo.ToTitleCase(normalized.ToLowerInvariant());
        return titleCaseString.Replace(" ", string.Empty);
    }
}

