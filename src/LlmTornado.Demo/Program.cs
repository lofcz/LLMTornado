using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using LlmTornado.Code;


namespace LlmTornado.Demo;

public class Program
{
    public static Keys ApiKeys { get; set; }

    public class AzureKey
    {
        public string Version { get; set; }
        public string ApiUrlFormat { get; set; }
        public string Key { get; set; }
    }
    
    public class Keys
    {
        public string OpenAi { get; set; }
        public string Anthropic { get; set; }
        public string Cohere { get; set; }
        public string Google { get; set; }
        public string Groq { get; set; }
        public AzureKey Azure { get; set; }
        public string DeepSeek { get; set; }
        public string Mistral { get; set; }
        public string XAi { get; set; }
        public string Perplexity { get; set; }
        public string Voyage { get; set; }
        public string DeepInfra { get; set; }
        public string OpenRouter { get; set; }
        public string AiFoundry { get; set; }
        public string MoonshotAi { get; set; }
        public string ZAi { get; set; }
        public string Blablador { get; set; }
        public string Alibaba { get; set; }
        public string Pinecone { get; set; }
        public string Requesty { get; set; }
        public string Upstage { get; set; }
        public string MiniMax { get; set; }
        public string LiteLlm { get; set; }
        public string TypeSafe { get; set; }
    }

    public static TornadoApi ConnectMulti(bool httpStrict = true)
    {
        TornadoApi tornadoApi = new TornadoApi([
            new ProviderAuthentication(LLmProviders.OpenAi, ApiKeys.OpenAi),
            new ProviderAuthentication(LLmProviders.Anthropic, ApiKeys.Anthropic),
            new ProviderAuthentication(LLmProviders.Cohere, ApiKeys.Cohere),
            new ProviderAuthentication(LLmProviders.Google, ApiKeys.Google),
            new ProviderAuthentication(LLmProviders.Groq, ApiKeys.Groq),
            new ProviderAuthentication(LLmProviders.DeepSeek, ApiKeys.DeepSeek),
            new ProviderAuthentication(LLmProviders.Mistral, ApiKeys.Mistral),
            new ProviderAuthentication(LLmProviders.XAi, ApiKeys.XAi),
            new ProviderAuthentication(LLmProviders.Voyage, ApiKeys.Voyage),
            new ProviderAuthentication(LLmProviders.DeepInfra, ApiKeys.DeepInfra),
            new ProviderAuthentication(LLmProviders.OpenRouter, ApiKeys.OpenRouter),
            new ProviderAuthentication(LLmProviders.Perplexity, ApiKeys.Perplexity),
            new ProviderAuthentication(LLmProviders.MoonshotAi, ApiKeys.MoonshotAi),
            new ProviderAuthentication(LLmProviders.Zai, ApiKeys.ZAi),
            new ProviderAuthentication(LLmProviders.Blablador, ApiKeys.Blablador),
            new ProviderAuthentication(LLmProviders.Alibaba, ApiKeys.Alibaba),
            new ProviderAuthentication(LLmProviders.Requesty, ApiKeys.Requesty),
            new ProviderAuthentication(LLmProviders.Upstage, ApiKeys.Upstage),
            new ProviderAuthentication(LLmProviders.MiniMax, ApiKeys.MiniMax),
            new ProviderAuthentication(LLmProviders.LiteLlm, ApiKeys.LiteLlm),
            new ProviderAuthentication(LLmProviders.TypeSafe, ApiKeys.TypeSafe)
        ])
        {
            HttpStrict = httpStrict
        };

        return tornadoApi;
    }

    public class TestRun
    {
        public MethodInfo Method { get; set; }
        public string? FriendlyName { get; set; }
        public Type Type { get; set; }
        public FlakyAttribute? Flaky { get; set; }
        public object[]? Arguments { get; set; }
        public string Name { get; set; }
    }
    
    public static TornadoApi Connect(bool httpStrict = true)
    {
        return ConnectMulti(httpStrict);
    }

    public static async Task<bool> SetupApi()
    {
        string apiKeyFileLocation = Path.Combine(Environment.CurrentDirectory, "src", "LlmTornado.Demo", "apiKey.json");

        if (!File.Exists(apiKeyFileLocation))
        {
             // Try looking in current directory
             apiKeyFileLocation = Path.Combine(Environment.CurrentDirectory, "apiKey.json");
        }

        if (!File.Exists(apiKeyFileLocation))
        {
             // Try the old logic
            string? projectDirectory = Directory.GetParent(Environment.CurrentDirectory)?.Parent?.Parent?.FullName;
            if (projectDirectory != null)
            {
                apiKeyFileLocation = Path.Join(projectDirectory, "apiKey.json");
            }
        }

        if (!File.Exists(apiKeyFileLocation))
        {
            Console.WriteLine($"API key file apiKey.json not found. Current Directory: {Environment.CurrentDirectory}");
            return false;
        }

        string apiKey = await File.ReadAllTextAsync(apiKeyFileLocation);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine("API key not set, please place your API key in apiKey.json file");
            return false;
        }

        ApiKeys = JsonConvert.DeserializeObject<Keys>(apiKey) ?? throw new Exception("Invalid content of apiKey.json");
        return true;
    }

    public static readonly Dictionary<string, TestRun> DemoDict = [];
    public static readonly List<Tuple<Type, Type>> DemoEnumTypes = [];
    static readonly Regex DemoTypeRegex = new Regex(@"Demo\d*$", RegexOptions.Compiled);
    
    static Program()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        Type[] types = assembly.GetTypes();

        foreach (Type type in types)
        {
            if (type.FullName is null)
            {
                continue;
            }
            
            object[] attrs = type.GetCustomAttributes(typeof(DemoEnumAttribute), false);

            if (attrs.Length > 0 && attrs[0] is DemoEnumAttribute dea)
            {
                DemoEnumTypes.Add(new Tuple<Type, Type>(type, dea.DemoType));
                continue;
            }

            if (DemoTypeRegex.IsMatch(type.FullName))
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    object[] testAttrs = method.GetCustomAttributes(typeof(TornadoTestAttribute), false);

                    if (testAttrs.Length > 0 && testAttrs[0] is TornadoTestAttribute tta)
                    {
                        object[] flaky = method.GetCustomAttributes(typeof(FlakyAttribute), false);
                        object[] testCaseAttrs = method.GetCustomAttributes(typeof(TornadoTestCaseAttribute), false);

                        if (testCaseAttrs.Length > 0)
                        {
                            foreach (object testCaseAttr in testCaseAttrs)
                            {
                                if (testCaseAttr is TornadoTestCaseAttribute tca)
                                {
                                    string testName = $"{type.FullName}.{method.Name}({string.Join(", ", tca.Arguments)})";
                                    DemoDict[testName] = new TestRun
                                    {
                                        Method = method,
                                        FriendlyName = tta.FriendlyName,
                                        Type = type,
                                        Flaky = flaky.Length is 0 ? null : flaky[0] as FlakyAttribute,
                                        Arguments = tca.Arguments,
                                        Name = testName
                                    };
                                }
                            }
                        }
                        else
                        {
                            string testName = $"{type.FullName}.{method.Name}";
                            DemoDict[testName] = new TestRun
                            {
                                Method = method,
                                FriendlyName = tta.FriendlyName,
                                Type = type,
                                Flaky = flaky.Length is 0 ? null : flaky[0] as FlakyAttribute,
                                Arguments = null,
                                Name = testName
                            };
                        }
                    }
                }   
            }
        }
    }

    static void ListDemos()
    {
        Console.Clear();
        Console.WriteLine($"Found {DemoDict.Count} demos.");

        int i = 1;
        
        foreach (KeyValuePair<string, TestRun> demo in DemoDict.OrderBy(x => x.Key, StringComparer.InvariantCultureIgnoreCase))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"({i}");

            if (demo.Value.FriendlyName?.Length > 0)
            {
                Console.Write($", {demo.Value.FriendlyName}");
            }
            
            Console.Write(")");
            Console.ResetColor();
            
            Console.Write($" {demo.Key}");
            Console.WriteLine();
            i++;
        }        
        
        Console.WriteLine();
        Console.WriteLine($"Enter number of the demo (yellow) / friendly name of the demo / method name of demo to run:");
    }

    static async Task Read()
    {
        ListDemos();
        
        string? toPlay = Console.ReadLine();

        // 1. try to interpret as numeric input
        if (int.TryParse(toPlay, out int demoN) && demoN > 0 && demoN <= DemoDict.Count)
        {
            KeyValuePair<string, TestRun> selected = DemoDict.OrderBy(x => x.Key, StringComparer.InvariantCultureIgnoreCase).Skip(demoN - 1).FirstOrDefault();
            await (Task)selected.Value.Method.Invoke(null, selected.Value.Arguments);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Demo finished");
            Console.ResetColor();
            Console.ReadKey();
            return;
        }

        // 2. try looking up by friendly name (grossly ineffective..)
        foreach (KeyValuePair<string, TestRun> x in DemoDict)
        {
            if (x.Value.FriendlyName is null)
            {
                continue;
            }

            if (string.Equals(x.Value.FriendlyName.Trim(), toPlay?.Trim()))
            {
                await (Task)x.Value.Method.Invoke(null, x.Value.Arguments);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Demo finished");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }
        }
        
        // 3. interpret the input as method name
        foreach (KeyValuePair<string, TestRun> x in DemoDict)
        {
            if (x.Value.Method.Name == toPlay?.Trim())
            {
                await (Task)x.Value.Method.Invoke(null, x.Value.Arguments);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Demo finished");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }
        }

        // 4. yell at user
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Invalid number, expected 1-{DemoDict.Count}");
        Console.ReadKey();
    }
    
    public static async Task Main(string[] args)
    {
        Console.Title = "LlmTornado Demo";
        
        if (!await SetupApi())
        {
            return;
        }

        while (true)
        {
            await Read();   
        }
    }
}
