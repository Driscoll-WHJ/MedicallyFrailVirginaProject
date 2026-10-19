using System.Text.Json;

namespace MES_EDWS.Controllers
{
    internal static class RequestBodyExtensions
    {
        /// <summary>
        /// Raw request body, made storable in an API_REQUEST.REQUEST_JSON (JSON type) column: valid
        /// JSON is kept as-is, anything else is wrapped as a JSON string literal so it is still recorded.
        /// Requires request buffering (enabled per route in Program.cs) because model binding has
        /// already consumed the stream.
        /// </summary>
        public static async Task<string?> ReadStorableJsonAsync(this HttpRequest request)
        {
            if (!request.Body.CanSeek)
                return null;

            request.Body.Position = 0;
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            var raw = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(raw))
                return null;

            try
            {
                using var _ = JsonDocument.Parse(raw);
                return raw;
            }
            catch (JsonException)
            {
                return JsonSerializer.Serialize(raw);
            }
        }
    }
}
