using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MES_EDWS.Controllers
{
    /// <summary>
    /// Walks a request DTO graph and returns the JSON path of every [Required] property that is
    /// missing, null, empty or whitespace (e.g. "ceVerified[0].addresses[1].city"), plus the
    /// fields named by any <see cref="IValidatableObject"/> rule (e.g. either-or requirements).
    /// </summary>
    /// <remarks>
    /// Used instead of MVC ModelState, which keys errors by C# property name, stops after
    /// MaxModelValidationErrors, and skips an object's IValidatableObject rule whenever one of its
    /// attributes has already failed.
    /// Required arrays only have to be present: the ICD's own payloads send [] for them.
    /// </remarks>
    public static class RequiredFieldValidator
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertiesByType = new();

        public static List<string> GetMissingFields(object model)
        {
            var missing = new List<string>();
            Visit(model, string.Empty, missing);
            return missing;
        }

        private static void Visit(object model, string path, List<string> missing)
        {
            var type = model.GetType();

            foreach (var property in ReadableProperties(type))
            {
                var value    = property.GetValue(model);
                var jsonPath = Join(path, JsonName(property));

                if (property.GetCustomAttribute<RequiredAttribute>(inherit: true) is { } required && !required.IsValid(value))
                    missing.Add(jsonPath);

                VisitChildren(value, jsonPath, missing);
            }

            if (model is IValidatableObject validatable)
            {
                foreach (var result in validatable.Validate(new ValidationContext(model)))
                {
                    var names = result.MemberNames.Select(name => type.GetProperty(name) is { } p ? JsonName(p) : name);
                    missing.Add(Join(path, string.Join(" or ", names)));
                }
            }
        }

        private static void VisitChildren(object? value, string path, List<string> missing)
        {
            if (value == null || value is string)
                return;

            if (IsModel(value.GetType()))
            {
                Visit(value, path, missing);
                return;
            }

            if (value is IEnumerable items)
            {
                var index = 0;
                foreach (var item in items)
                {
                    if (item != null && IsModel(item.GetType()))
                        Visit(item, $"{path}[{index}]", missing);
                    index++;
                }
            }
        }

        private static bool IsModel(Type type) =>
            type.IsClass && type != typeof(string) && type.Assembly == typeof(RequiredFieldValidator).Assembly;

        private static PropertyInfo[] ReadableProperties(Type type) =>
            PropertiesByType.GetOrAdd(type, t => t
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetMethod != null && p.GetIndexParameters().Length == 0)
                .ToArray());

        private static string JsonName(PropertyInfo property) =>
            property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
            ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);

        private static string Join(string path, string name) =>
            path.Length == 0 ? name : $"{path}.{name}";
    }
}
