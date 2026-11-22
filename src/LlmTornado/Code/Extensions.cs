using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LlmTornado.Code;

internal static partial class Extensions
{
    public static string StripDataUriPrefix(this string base64Data)
    {
        if (!base64Data.StartsWith("data:"))
        {
            return base64Data;
        }
        
        int commaIndex = base64Data.IndexOf(',');
        return commaIndex != -1 ? base64Data[(commaIndex + 1)..] : base64Data;
    }
    
    public static void Serialize(this JsonWriter writer, object? obj)
    {
        if (obj is null)
        {
            return;
        }
        
        JToken cacheToken = JToken.FromObject(obj);
        cacheToken.WriteTo(writer);
    }
    
    #if MODERN
    public static Dictionary<string, IEnumerable<string>> ConvertHeaders(this HttpRequestHeaders headers)
    {
        return headers.ToDictionary(h => h.Key, h => h.Value);
    }
    
    public static double Clamp(this double value, double min, double max)
    {
        if (value < min)
            return min;
        return value > max ? max : value;
    }
    
    public static float Clamp(this float value, float min, float max)
    {
        if (value < min)
            return min;
        return value > max ? max : value;
    }
    
    public static int Clamp(this int value, int min, int max)
    {
        if (value < min)
            return min;
        return value > max ? max : value;
    }
    
    public static long Clamp(this long value, long min, long max)
    {
        if (value < min)
            return min;
        return value > max ? max : value;
    }
    
    public static string ReplaceFirst(this string text, string search, string replace)
    {
        int pos = text.IndexOf(search, StringComparison.InvariantCulture);
        return pos < 0 ? text : string.Concat(text.AsSpan(0, pos), replace, text.AsSpan(pos + search.Length));
    }
    #endif
    
    public static JObject ToJObject(this object sourceObject, JsonSerializerSettings? settings = null)
    {
        JsonSerializer serializer = JsonSerializer.CreateDefault(settings ?? EndpointBase.NullSettings);
        return JObject.FromObject(sourceObject, serializer);
    }

    public static string SerializeRequestObject(this object sourceObject, object refObject, IEndpointProvider provider, RequestActionTypes action, JsonSerializerSettings? settings = null)
    {
        JObject jObj = sourceObject.ToJObject();
        provider.RequestSerializer?.Invoke(jObj, new RequestSerializerContext(refObject, provider, action));
        return jObj.ToString(settings?.Formatting ?? Formatting.None);
    }
    
    public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this object source)
    {
        Dictionary<TKey, TValue> result = new Dictionary<TKey, TValue>();

        TKey[] keys = [];
        TValue[] values = [];

        bool outLoopingKeys = false, outLoopingValues = false;

        foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(source))
        {
            object value = property.GetValue(source);
            
            switch (value)
            {
                case Dictionary<TKey, TValue>.KeyCollection collection:
                {
                    keys = collection.ToArray<TKey>();
                    outLoopingKeys = true;
                    break;
                }
                case Dictionary<TKey, TValue>.ValueCollection valueCollection:
                {
                    values = valueCollection.ToArray<TValue>();
                    outLoopingValues = true;
                    break;
                }
            }

            if (outLoopingKeys & outLoopingValues)
            {
                break;
            }
        }

        for (int i = 0; i < keys.Length; i++)
        {
            result.Add(keys[i], values[i]);
        }

        return result;
    }
    
    private static readonly ConcurrentDictionary<string, string?> DescriptionAttrCache = [];

    public static HttpMethod ToMethod(this HttpVerbs verb)
    {
        return verb switch
        {
            HttpVerbs.Get => HttpVerbsCls.Get,
            HttpVerbs.Head => HttpVerbsCls.Head,
            HttpVerbs.Post => HttpVerbsCls.Post,
            HttpVerbs.Put => HttpVerbsCls.Put,
            HttpVerbs.Delete => HttpVerbsCls.Delete,
            HttpVerbs.Options => HttpVerbsCls.Options,
            HttpVerbs.Trace => HttpVerbsCls.Trace,
            HttpVerbs.Patch => HttpVerbsCls.Patch,
            HttpVerbs.Connect => HttpVerbsCls.Connect,
            _ => HttpMethod.Get
        };
    }
    
    internal static bool IsNullOrWhiteSpace([NotNullWhen(returnValue: false)] this string? str)
    {
        return string.IsNullOrWhiteSpace(str);
    }
    
    public static string? ToCsv(this IEnumerable? elems, string separator = ",")
    {
        if (elems == null)
        {
            return null;
        }

        StringBuilder sb = new StringBuilder();
        foreach (object elem in elems)
        {
            if (sb.Length > 0)
            {
                sb.Append(separator);
            }

            if (elem is Enum)
            {
                sb.Append((int)elem);
            }
            else
            {
                sb.Append(elem);   
            }
        }

        return sb.ToString();
    }
    
    public static void AddOrUpdate<TK, TV>(this ConcurrentDictionary<TK, TV> dictionary, TK key, TV value) where TK : notnull
    {
        dictionary.AddOrUpdate(key, value, (k, v) => value);
    }
    
    public static JsonSerializerSettings DeepCopy(this JsonSerializerSettings serializer)
    {
        JsonSerializerSettings copiedSerializer = new JsonSerializerSettings
        {
            Context = serializer.Context,
            Culture = serializer.Culture,
            ContractResolver = serializer.ContractResolver,
            ConstructorHandling = serializer.ConstructorHandling,
            CheckAdditionalContent = serializer.CheckAdditionalContent,
            DateFormatHandling = serializer.DateFormatHandling,
            DateFormatString = serializer.DateFormatString,
            DateParseHandling = serializer.DateParseHandling,
            DateTimeZoneHandling = serializer.DateTimeZoneHandling,
            DefaultValueHandling = serializer.DefaultValueHandling,
            EqualityComparer = serializer.EqualityComparer,
            FloatFormatHandling = serializer.FloatFormatHandling,
            Formatting = serializer.Formatting,
            FloatParseHandling = serializer.FloatParseHandling,
            MaxDepth = serializer.MaxDepth,
            MetadataPropertyHandling = serializer.MetadataPropertyHandling,
            MissingMemberHandling = serializer.MissingMemberHandling,
            NullValueHandling = serializer.NullValueHandling,
            ObjectCreationHandling = serializer.ObjectCreationHandling,
            PreserveReferencesHandling = serializer.PreserveReferencesHandling,
            ReferenceLoopHandling = serializer.ReferenceLoopHandling,
            StringEscapeHandling = serializer.StringEscapeHandling,
            TraceWriter = serializer.TraceWriter,
            TypeNameHandling = serializer.TypeNameHandling,
            SerializationBinder = serializer.SerializationBinder,
            TypeNameAssemblyFormatHandling = serializer.TypeNameAssemblyFormatHandling
        };
        
        foreach (JsonConverter converter in serializer.Converters)
        {
            copiedSerializer.Converters.Add(converter);
        }
        
        return copiedSerializer;
    }
    
    public static async Task<byte[]> ToArrayAsync(this Stream stream)
    {
        if (stream is MemoryStream memorySource)
        {
            return memorySource.ToArray();
        }
        
        using MemoryStream memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }
    
    public static bool CaptureJsonDecode<T>(this string? s, out T? data, out Exception? parseException)
    {
        if (string.IsNullOrEmpty(s))
        {
            data = default;
            parseException = null;
            return true;
        }

        try
        {
            data = JsonConvert.DeserializeObject<T>(s);
            parseException = null;
            return true;
        }
        catch (Exception e)
        {
            data = default;
            parseException = e;
            return false;
        }
    }
    
    public static object? ChangeType(this object? value, Type conversion) 
    {
        Type? t = conversion;

        if (t.IsEnum && value != null)
        {
            if (EnumsParser.TryParse(t, value.ToString(), true, out object? x))
            {
                return x;
            }
        }
            
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>)) 
        {
            if (value == null) 
            { 
                return null; 
            }

            t = Nullable.GetUnderlyingType(t);
        }

        if (t == typeof(int) && value?.ToString() == "")
        {
            return 0;
        }
            
        if (t == typeof(int) && ((value?.ToString()?.Contains('.') ?? false) || (value?.ToString()?.Contains(',') ?? false)))
        {
            if (double.TryParse(value.ToString()?.Replace(",", "."), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double x))
            {
                return (int)x;
            }
        }

        if (value != null && t is {IsGenericType: true} && value.GetType().IsGenericType)
        {
            Type destT = t.GetGenericArguments()[0];
            Type sourceT = value.GetType().GetGenericArguments()[0];

            if (destT.IsEnum && sourceT == typeof(int))
            {
                IList? instance = (IList?)t.Instantiate();

                foreach (object? x in (IList) value)
                {
                    instance?.Add(x);
                }

                return instance;
            }
        }

        if (t == typeof(bool) && value is string str)
        {
            return truthyStrings.Contains(str);
        }

        return t != null ? System.Convert.ChangeType(value, t) : null;
    }
    
    public static object? Instantiate(this Type type, params object[] args)
    {
        return Activator.CreateInstance(type, args);
    }
    
    private static readonly HashSet<string> truthyStrings = ["1", "on", "true", "True", "TRUE", "yes", "ok", "success"];
    
    public static string SanitizeJsonTrailingComma(this string json)
    {
        string originalJson = json;
        string trimmedStart = json.TrimStart();
            
        if (trimmedStart.StartsWith('{') || trimmedStart.StartsWith('['))
        {
            json = trimmedStart;
        }

        json = json.TrimEnd();
            
        if (json.EndsWith(','))
        {
            json = json.TrimEnd(',');
        }
            
        return json != originalJson ? json : originalJson;
    }
    
    public static string? GetDescription<T>(this T source)
    {
        if (source is null)
        {
            return null;
        }

        if (DescriptionAttrCache.TryGetValue($"{source.GetType()}_{source.ToString()}", out string? val))
        {
            return val;
        }
        
        FieldInfo? fi = source.GetType().GetField(source.ToString() ?? string.Empty);

        if (fi is null)
        {
            DescriptionAttrCache.TryAdd($"{source.GetType()}_{source.ToString()}", null);
            return null;
        }
        
        DescriptionAttribute[] attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
        string? value = attributes is { Length: > 0 } ? attributes[0].Description : source.ToString();
        DescriptionAttrCache.TryAdd($"{source.GetType()}_{source.ToString()}", value);
        
        return value;
    }
    
    /// <summary>
    /// Converts an object to a dictionary by serializing it.
    /// </summary>
    public static Dictionary<string, object?>? ToDictionary(this object obj)
    {
        JObject jObject = JObject.FromObject(obj);
        return jObject.ToObject<Dictionary<string, object?>>();
    }
    
    private static readonly JsonSerializerSettings JsonSettingsIgnoreNulls = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
    
    public static string ToJson(this object? obj, bool prettify = false)
    {
        return obj is null ? "{}" : JsonConvert.SerializeObject(obj, prettify ? Formatting.Indented : Formatting.None, JsonSettingsIgnoreNulls);
    }
    
    public static T? JsonDecode<T>(this string? obj)
    {
        return obj is null ? default : JsonConvert.DeserializeObject<T>(obj);
    }
    
    public static void AddOrUpdate<TKey, TVal>(this Dictionary<TKey, TVal> dict, TKey key, TVal val)
    {
        dict[key] = val;
    }
}