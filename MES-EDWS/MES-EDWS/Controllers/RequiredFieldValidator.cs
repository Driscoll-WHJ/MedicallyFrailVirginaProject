using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MES_EDWS.Models;

namespace MES_EDWS.Controllers
{
    /// <summary>
    /// Walks the raw request JSON alongside its DTO type and returns the JSON path of every [Required]
    /// element that is absent (e.g. "ceVerified[0].addresses[1].city"), plus every
    /// <see cref="RequiredElementAttribute"/> rule with none of its elements present.
    /// </summary>
    /// <remarks>
    /// Required means the element must be sent; its value may be null or "". An [AliasOf] property's
    /// name counts as its target being sent. Keys match case-insensitively, as in model binding.
    /// Used instead of MVC ModelState, which can only see deserialized values, so it cannot tell an
    /// absent element from one sent as null.
    /// </remarks>
    public static class RequiredFieldValidator
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertiesByType = new();

        public static List<string> GetMissingFields(JsonElement json, Type modelType)
        {
            var missing = new List<string>();
            Visit(json, modelType, string.Empty, missing);
            return missing;
        }

        private static void Visit(JsonElement json, Type type, string path, List<string> missing)
        {
            if (json.ValueKind != JsonValueKind.Object)
                return;

            var properties = JsonProperties(type);
            foreach (var property in properties)
            {
                var name     = JsonName(property);
                var jsonPath = Join(path, name);
                var element  = Find(json, name);

                if (element is { } value)
                    VisitChildren(value, property.PropertyType, jsonPath, missing);
                else if (property.GetCustomAttribute<RequiredAttribute>(inherit: true) != null &&
                         !properties.Any(alias => alias.GetCustomAttribute<AliasOfAttribute>()?.Target == property.Name &&
                                                  Find(json, JsonName(alias)) != null))
                    missing.Add(jsonPath);
            }

            foreach (var rule in type.GetCustomAttributes<RequiredElementAttribute>(inherit: true))
            {
                if (!rule.AnyOf.Any(name => Find(json, name) != null))
                    missing.Add(Join(path, string.Join(" or ", rule.AnyOf)));
            }
        }

        private static void VisitChildren(JsonElement json, Type type, string path, List<string> missing)
        {
            if (json.ValueKind == JsonValueKind.Object && IsModel(type))
            {
                Visit(json, type, path, missing);
                return;
            }

            if (json.ValueKind == JsonValueKind.Array && ElementType(type) is { } itemType && IsModel(itemType))
            {
                var index = 0;
                foreach (var item in json.EnumerateArray())
                    Visit(item, itemType, $"{path}[{index++}]", missing);
            }
        }

        private static JsonElement? Find(JsonElement json, string name)
        {
            foreach (var property in json.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    return property.Value;
            }
            return null;
        }

        private static Type? ElementType(Type type) =>
            type.IsArray
                ? type.GetElementType()
                : type.GetInterfaces().Append(type)
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    ?.GetGenericArguments()[0];

        private static bool IsModel(Type type) =>
            type.IsClass && type != typeof(string) && type.Assembly == typeof(RequiredFieldValidator).Assembly;

        private static PropertyInfo[] JsonProperties(Type type) =>
            PropertiesByType.GetOrAdd(type, t => t
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                .ToArray());

        private static string JsonName(PropertyInfo property) =>
            property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
            ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);

        private static string Join(string path, string name) =>
            path.Length == 0 ? name : $"{path}.{name}";
    }
}
