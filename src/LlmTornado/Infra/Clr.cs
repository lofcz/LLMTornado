using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Code;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace LlmTornado.Infra;

/// <summary>
/// Result of invoking a method.
/// </summary>
public class MethodInvocationResult
{
    /// <summary>
    /// Result returned by the method, if any.
    /// </summary>
    public object? Result { get; set; }
    
    /// <summary>
    /// Captured exception during invocation, null if invocation is successful.
    /// </summary>
    public Exception? InvocationException { get; set; }
    
    /// <summary>
    /// Whether the invocation ran without errors.
    /// </summary>
    public bool InvocationSuccessful { get; set; }

    /// <summary>
    /// A failed invocation result, containing the exception that occurred.
    /// </summary>
    public MethodInvocationResult(Exception exception)
    {
        InvocationException = exception;
    }

    /// <summary>
    /// A successful invocation result, containing the value returned by the method.
    /// </summary>
    public MethodInvocationResult(object? result)
    {
        Result = result;
        InvocationSuccessful = true;
    }
}

internal static class Clr
{
    private static readonly JsonSerializer serializer = new JsonSerializer
    {
        ContractResolver = new SchemaNameContractResolver()
    };
    
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> taskResultPropertyCache = new ConcurrentDictionary<Type, PropertyInfo?>();
    private static readonly ConcurrentDictionary<(ICustomAttributeProvider Provider, Type AttributeType), Attribute?> customAttributeCache = new ConcurrentDictionary<(ICustomAttributeProvider, Type), Attribute?>();
    private static readonly ConcurrentDictionary<(Type Type, string MethodName, Type[] ParameterTypes), MethodInfo?> methodInfoCache = new ConcurrentDictionary<(Type, string, Type[]), MethodInfo?>();
    
    private static MethodInfo? GetCachedMethod(Type type, string methodName, BindingFlags bindingFlags, Type[]? parameterTypes)
    {
        Type[] paramTypes = parameterTypes ?? [];
        return methodInfoCache.GetOrAdd((type, methodName, paramTypes), key => 
            key.Type.GetMethod(key.MethodName, bindingFlags, null, key.ParameterTypes, null));
    }
    
    private static TAttribute? GetCachedCustomAttribute<TAttribute>(ICustomAttributeProvider provider) where TAttribute : Attribute
    {
        return (TAttribute?)customAttributeCache.GetOrAdd((provider, typeof(TAttribute)), key => 
        {
            return key.Provider switch
            {
                ParameterInfo param => param.GetCustomAttribute<TAttribute>(),
                PropertyInfo prop => prop.GetCustomAttribute<TAttribute>(),
                MemberInfo member => member.GetCustomAttribute<TAttribute>(),
                _ => (key.Provider.GetCustomAttributes(typeof(TAttribute), false) as Attribute[] ?? []).FirstOrDefault() as TAttribute
            };
        });
    }

    private static object? DeserializePrimitive(JToken token, Type dataType)
    {
        if (dataType == typeof(Guid))
        {
            return Guid.Parse(token.ToString());
        }

        if (dataType == typeof(byte[]))
        {
            if (token is JArray jArray)
            {
                byte[] result = new byte[jArray.Count];
                
                for (int i = 0; i < jArray.Count; i++)
                {
                    result[i] = jArray[i].Value<byte>();
                }
                
                return result;
            }
            
            string byteString = token.ToString();

            try
            {
                return Convert.FromBase64String(byteString);
            }
            catch (FormatException)
            {
                return Encoding.GetEncoding("iso-8859-1").GetBytes(byteString);
            }
        }
        
        if (dataType == typeof(TimeSpan))
        {
            string timeSpanString = token.ToString();
            
            if (TimeSpan.TryParse(timeSpanString, CultureInfo.InvariantCulture, out TimeSpan parsedTimeSpan))
            {
                return parsedTimeSpan;
            }
            
            try
            {
                return System.Xml.XmlConvert.ToTimeSpan(timeSpanString);
            }
            catch (FormatException)
            {
                throw new JsonException($"The string '{timeSpanString}' could not be parsed as a valid TimeSpan. It must be in a common format (e.g., 'd.hh:mm:ss') or the ISO 8601 duration format (e.g., 'P1DT2H').");
            }
        }

        if (dataType == typeof(Uri))
        {
            return new Uri(token.ToString(), UriKind.RelativeOrAbsolute);
        }

        if (dataType == typeof(Regex))
        {
            return new Regex(token.ToString());
        }

        if (dataType == typeof(char))
        {
            return token.ToString()[0];
        }
        
#if MODERN
        if (dataType == typeof(Rune))
        {
            return new Rune(token.ToString()[0]);
        }
#endif
        
        return token.ToObject(dataType, serializer);
    }

    private static object? DeserializeObject(JToken token, Type dataType)
    {
        return token.ToObject(dataType, serializer);
    }

    private static ArrayList DeserializeNonGenericEnumerable(JToken token)
    {
        ArrayList list = new ArrayList();
        
        if (token is JArray jArray)
        {
            foreach (JToken item in jArray)
            {
                list.Add(item.ToObject<object>()!);
            }
        }

        return list;
    }

    private static Array? DeserializeMdArray(JToken token, Type dataType)
    {
        if (token is JObject mdArrayObject &&
            mdArrayObject.TryGetValue("lengths", StringComparison.OrdinalIgnoreCase, out JToken? lengthsToken) &&
            mdArrayObject.TryGetValue("values", StringComparison.OrdinalIgnoreCase, out JToken? valuesToken))
        {
            int[]? lengths = lengthsToken.ToObject<int[]>();
            Type? elementType = dataType.GetElementType();

            if (lengths is not null && elementType is not null)
            {
                Array mdArray = Array.CreateInstance(elementType, lengths);
                JArray valuesArray = (JArray)valuesToken;
                int[] indices = new int[lengths.Length];

                for (int i = 0; i < valuesArray.Count; i++)
                {
                    int linearIndex = i;
                    for (int dim = lengths.Length - 1; dim >= 0; dim--)
                    {
                        indices[dim] = linearIndex % lengths[dim];
                        linearIndex /= lengths[dim];
                    }

                    mdArray.SetValue(valuesArray[i].ToObject(elementType, serializer), indices);
                }

                return mdArray;
            }
        }

        return null;
    }
    
    private static object DeserializeDictionary(JToken token, Type dataType)
    {
        IDictionary dict = (IDictionary)Activator.CreateInstance(dataType)!;
        Type[] genericArgs = dataType.GetGenericArguments();
        Type keyType = genericArgs[0];
        Type valueType = genericArgs[1];

        if (token is JArray array)
        {
            foreach(JObject item in array.Children<JObject>())
            {
                if (item.TryGetValue("key", StringComparison.OrdinalIgnoreCase, out JToken? keyToken) && item.TryGetValue("value", StringComparison.OrdinalIgnoreCase, out JToken? valueToken))
                {
                    object? key = keyToken.ToObject(keyType, serializer);
                    object? val = valueToken.ToObject(valueType, serializer);
                                    
                    if (key is not null)
                    {
                        dict.Add(key, val);
                    }
                }
            }
        }
        
        return dict;
    }
    
    private static object CreateCollection(Type dataType, Type elementType, IList intermediateValues)
    {
        if (dataType.IsArray)
        {
            Array finalArray = Array.CreateInstance(elementType, intermediateValues.Count);
            intermediateValues.CopyTo(finalArray, 0);
            return finalArray;
        }
        
        if (dataType.IsInstanceOfType(intermediateValues))
        {
            return intermediateValues;
        }
        
        Type defaultConcreteType = dataType.IsISet() ? typeof(HashSet<>).MakeGenericType(elementType) : typeof(List<>).MakeGenericType(elementType);
        Type concreteType = (dataType.IsInterface || dataType.IsAbstract) ? defaultConcreteType : dataType;
        object finalCollection;

        try
        {
            finalCollection = Activator.CreateInstance(concreteType, intermediateValues)!;
        }
        catch (MissingMethodException)
        {
            finalCollection = Activator.CreateInstance(concreteType)!;
            MethodInfo? addMethod = GetCachedMethod(concreteType, "Add", BindingFlags.Public | BindingFlags.Instance, [elementType]);
            addMethod ??= GetCachedMethod(concreteType, "TryAdd", BindingFlags.Public | BindingFlags.Instance, [elementType]);
            
            if (addMethod is not null)
            {
                foreach (object? listItem in intermediateValues)
                {
                    addMethod.Invoke(finalCollection, [listItem]);
                }
            }
            else
            {
                throw new NotSupportedException($"The collection type '{dataType.Name}' is not supported. It must either have a constructor that accepts an IEnumerable<T> or a public 'Add(T)'/'TryAdd(T)' method.");
            }
        }
        
        return finalCollection;
    }
    
    public static async ValueTask<MethodInvocationResult> Invoke(Delegate? function, DelegateMetadata? metadata, string? data, CancellationToken cancellationToken = default)
    {
        if (function is null || metadata?.Tool?.Params is null)
        {
            return new MethodInvocationResult(new Exception("Function is null or no params are defined in the metadata"));
        }

        object? result = null;
        List<object?> args = [];
        string normalizedData = data ?? "{}";
        JObject jObject;
        
        try
        {
            jObject = JObject.Parse(normalizedData);
        }
        catch (Exception e)
        {
            return new MethodInvocationResult(new Exception($"Model responded with invalid JSON:\n{normalizedData}", e));
        }
        
        ParameterInfo[] delegateParams = function.Method.GetParameters();
        Dictionary<string, (string csName, ToolParam Param)> toolParamsMap = new Dictionary<string, (string csName, ToolParam Param)>(metadata.Tool.Params.Count);

        foreach (ToolParam p in metadata.Tool.Params)
        {
            toolParamsMap.Add(p.Name, (string.Empty, p));
        }

        foreach (ParameterInfo delegateParam in delegateParams)
        {
            if (delegateParam.Name is null)
            {
                continue;
            }

            SchemaNameAttribute? schemaName = GetCachedCustomAttribute<SchemaNameAttribute>(delegateParam);
            string finalName = schemaName?.Name ?? delegateParam.Name;

            if (toolParamsMap.TryGetValue(finalName, out (string csName, ToolParam Param) val))
            {
                val.csName = delegateParam.Name;
                toolParamsMap[finalName] = val;
            }
        }

        foreach (ParameterInfo delegateParam in delegateParams)
        {
            if (delegateParam.Name is null)
            {
                args.Add(null);
                continue;
            }
            
            if (delegateParam.ParameterType == typeof(ToolArguments))
            {
                args.Add(new ToolArguments { Data = normalizedData });
                continue;
            }

            if (delegateParam.ParameterType == typeof(CancellationToken))
            {
                args.Add(cancellationToken);
                continue;
            }

            SchemaNameAttribute? schemaName = GetCachedCustomAttribute<SchemaNameAttribute>(delegateParam);
            string finalName = schemaName?.Name ?? delegateParam.Name;

            if (toolParamsMap.TryGetValue(finalName, out (string csName, ToolParam Param) toolParam) && 
                jObject.TryGetValue(toolParam.Param.Name, StringComparison.OrdinalIgnoreCase, out JToken? token))
            {
                args.Add(Deserialize(token, toolParam.Param.Type));
            }
            else
            {
                args.Add(delegateParam.IsOptional ? Type.Missing : null);
            }
        }
            
        try
        {
#if MODERN
            object?[] argsArray = CollectionsMarshal.AsSpan(args).ToArray();
#else
            object?[] argsArray = new object?[delegateParams.Length];
            args.CopyTo(argsArray, 0);
#endif
            object? invocationResult = function.DynamicInvoke(argsArray);
                
            if (invocationResult is Task task)
            {
                await task.ConfigureAwait(false);
                Type taskType = task.GetType();
                
                if (taskType.IsGenericType)
                {
                    PropertyInfo? resultProperty = taskResultPropertyCache.GetOrAdd(taskType, type => type.GetProperty("Result"));
            
                    if (resultProperty is not null)
                    {
                        result = resultProperty.GetValue(task);
                    }
                }
            }
            else if (invocationResult is not null)
            {
                Type type = invocationResult.GetType();
                
                if (type == typeof(ValueTask) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>)))
                {
                    result = await (dynamic)invocationResult;
                }
                else
                {
                    result = invocationResult;
                }
            }

            return new MethodInvocationResult(result);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // DynamicInvoke wraps whatever the tool threw, report the tool's own exception
            return new MethodInvocationResult(ex.InnerException);
        }
        catch (Exception ex)
        {
            return new MethodInvocationResult(ex);
        }
    }

    private static object? Deserialize(JToken token, IToolParamType paramType)
    {
        if (token.Type == JTokenType.Null)
        {
            return null;
        }

        if (paramType is ToolParamNullable nullable)
        {
            return Deserialize(token, nullable.InnerType);
        }

        Type? dataType = paramType.DataType;
        
        if (dataType is null)
        {
            return null;
        }
        
        switch (paramType.Serializer)
        {
            case ToolParamSerializer.Dictionary:
                return DeserializeDictionary(token, dataType);
            case ToolParamSerializer.Set:
            case ToolParamSerializer.Array:
                return paramType switch
                {
                    ToolParamListEnum listEnumParam => DeserializeListEnum(token, dataType, listEnumParam),
                    ToolParamListAtomic listAtomicParam => DeserializeListAtomic(token, dataType, listAtomicParam),
                    ToolParamList listParam => DeserializeListPolymorphic(token, dataType, listParam),
                    _ => DeserializeObject(token, dataType)
                };
            case ToolParamSerializer.Awaitable:
                if (paramType is ToolParamAwaitable awaitableParam)
                {
                    object? innerValue = Deserialize(token, awaitableParam.InnerParam);
                    
                    if (dataType == typeof(Task)) return Task.FromResult(innerValue);
                    if (dataType == typeof(ValueTask)) return new ValueTask();
                    if (dataType!.IsGenericType)
                    {
                        if (dataType.GetGenericTypeDefinition() == typeof(Task<>))
                        {
                            MethodInfo fromResult = typeof(Task).GetMethod("FromResult")!.MakeGenericMethod(dataType.GetGenericArguments()[0]);
                            return fromResult.Invoke(null, [innerValue]);
                        }

                        if (dataType.GetGenericTypeDefinition() == typeof(ValueTask<>))
                        {
                            return Activator.CreateInstance(dataType, innerValue);
                        }
                    }
                }
                return null;
            case ToolParamSerializer.Tuple: 
                return DeserializeTuple(token, (ToolParamTuple)paramType);
            case ToolParamSerializer.Json:
                if (dataType == typeof(JToken) || dataType.IsSubclassOf(typeof(JToken)))
                {
                    return token;
                }
                
#if MODERN
                if (dataType == typeof(System.Text.Json.JsonElement))
                {
                    return System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(token.ToString(Formatting.None));
                }
#endif
                
                return token;
            case ToolParamSerializer.MultidimensionalArray:
                return DeserializeMdArray(token, dataType);
            case ToolParamSerializer.NonGenericEnumerable:
                return DeserializeNonGenericEnumerable(token);
            case ToolParamSerializer.Object:
                return DeserializeObject(token, dataType);
            case ToolParamSerializer.Atomic:
                return DeserializePrimitive(token, dataType);
            case ToolParamSerializer.Any:
                        if (dataType == typeof(ExpandoObject))
        {
            return token.ToObject<ExpandoObject>();
        }
        
        return token.ToObject<object>();
    case ToolParamSerializer.AnyOf:
        if (paramType is ToolParamAnyOf anyOfParam)
        {
            return DeserializeAnyOf(token, anyOfParam);
        }

        return null;
    case ToolParamSerializer.Undefined:
    default:
        return token.ToObject(dataType, serializer);
}
    }

    private static object? DeserializeTuple(JToken token, ToolParamTuple tupleParam)
    {
        if (token is not JObject obj) throw new JsonException("Expected a JSON object for tuple deserialization.");

        List<object?> items = [];
        for (int i = 0; i < tupleParam.Items.Count; i++)
        {
            string key = tupleParam.Names?.Count == tupleParam.Items.Count ? tupleParam.Names[i] : $"item_{i + 1}";
            
            if (obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken? itemToken))
            {
                items.Add(Deserialize(itemToken, tupleParam.Items[i]));
            }
            else
            {
                Type? itemType = tupleParam.Items[i].DataType;
                items.Add(itemType is not null && itemType.IsValueType ? Activator.CreateInstance(itemType) : null);
            }
        }

        return Activator.CreateInstance(tupleParam.DataType!, items.ToArray());
    }

    private static object? DeserializeListEnum(JToken token, Type dataType, ToolParamListEnum param)
    {
        Type? elementType = dataType.IsArray ? dataType.GetElementType() : (dataType.IsGenericType ? dataType.GetGenericArguments()[0] : null);
        if (elementType is null) { return DeserializeObject(token, dataType); }

        Type underlyingEnumType = ToolFactory.GetNullableBaseType(elementType).Item1;
    
        Type listType = typeof(List<>).MakeGenericType(elementType);
        IList intermediateList = (IList)Activator.CreateInstance(listType)!;

        if (token is JArray jArray)
        {
            foreach (JToken item in jArray)
            {
                if (item.Type is JTokenType.String)
                {
                    intermediateList.Add(Enum.Parse(underlyingEnumType, item.Value<string>()!));
                }
            }
        }
        
        return CreateCollection(dataType, elementType, intermediateList);
    }
    
    private static object? DeserializeListAtomic(JToken token, Type dataType, ToolParamListAtomic param)
    {
        Type? elementType = dataType.IsArray ? dataType.GetElementType() : (dataType.IsGenericType ? dataType.GetGenericArguments()[0] : null);
        if (elementType is null) { return DeserializeObject(token, dataType); }
    
        Type listType = typeof(List<>).MakeGenericType(elementType);
        IList intermediateList = (IList)Activator.CreateInstance(listType)!;

        if (token is JArray jArray)
        {
            foreach (JToken item in jArray)
            {
                intermediateList.Add(item.ToObject(elementType));
            }
        }
        
        return CreateCollection(dataType, elementType, intermediateList);
    }
    
    private static object? DeserializeListPolymorphic(JToken token, Type dataType, ToolParamList listParam)
    {
        Type? elementType = dataType.IsArray ? dataType.GetElementType() : (dataType.IsGenericType ? dataType.GetGenericArguments()[0] : null);

        if (elementType is not null)
        {
            Type listType = typeof(List<>).MakeGenericType(elementType);
            IList intermediateList = (IList)Activator.CreateInstance(listType)!;

            if (token is JArray jArray)
            {
                foreach (JToken item in jArray)
                {
                    intermediateList.Add(Deserialize(item, listParam.Items));
                }
            }
            
            return CreateCollection(dataType, elementType, intermediateList);
        }

        return DeserializeObject(token, dataType);
    }

    private static object? DeserializeAnyOf(JToken token, ToolParamAnyOf anyOfParam)
    {
        if (token is not JObject jObject)
        {
            throw new JsonException("Expected a JSON object for 'anyOf' deserialization, but received a different type.");
        }

        string? typeName = null;
        
        foreach (string key in ToolDefaults.DiscriminatorKeys)
        {
            if (jObject.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken? discriminatorToken))
            {
                typeName = discriminatorToken.Value<string>();
                break;
            }
        }
        
        if (typeName is null)
        {
            throw new JsonException($"Required discriminator property not found on object for 'anyOf' deserialization. Searched for: {string.Join(", ", ToolDefaults.DiscriminatorKeys)}");
        }
        
        Type? targetType = anyOfParam.PossibleTypes.FirstOrDefault(t => t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));

        if (targetType is null)
        {
            throw new JsonException($"Received discriminator value '{typeName}' does not match any of the possible types for this 'anyOf' parameter.");
        }

        return token.ToObject(targetType, serializer);
    }
    
    /// <summary>
    /// Gets the specified argument.
    /// </summary>
    public static bool Get<T>(string param, Dictionary<string, object?> arguments, out T? data, out Exception? exception)
    {
        exception = null;

        if (!arguments.TryGetValue(param, out object? rawData))
        {
            data = default;
            return false; 
        }

        if (rawData is T obj)
        {
            data = obj;
            return true;
        }

        switch (rawData)
        {
            case JArray jArr:
            {
                data = jArr.ToObject<T?>(serializer);
                return true;
            }
            case JObject jObj:
            {
                data = jObj.ToObject<T?>(serializer);
                return true;
            }
            case string str:
            {
                if (typeof(T).IsClass || (typeof(T).IsValueType && !typeof(T).IsPrimitive && !typeof(T).IsEnum))
                {
                    if (str.SanitizeJsonTrailingComma().CaptureJsonDecode(out T? decoded, out Exception? parseException))
                    {
                        data = decoded;
                        return true;
                    }
                }
                
                try
                {
                    data = (T?)rawData.ChangeType(typeof(T));
                    return true;
                }
                catch (Exception e)
                {
                    data = default;
                    exception = e;
                    return false;
                }
            }
            default:
            {
                try
                {
                    data = (T?)rawData.ChangeType(typeof(T));
                    return true;
                }
                catch (Exception e)
                {
                    data = default;
                    exception = e;
                    return false;
                }
            }
        }
    }
}
