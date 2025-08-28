using System.IO;
using System.Text.Json;

namespace PASS.Web.Services
{
    public class ContentService
    {
        private JsonElement _jsonData;
        private readonly string _jsonFilePath;

        public ContentService()
        {
            _jsonFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");
            LoadJson();
        }

        private void LoadJson()
        {
            var jsonText = File.ReadAllText(_jsonFilePath);
            _jsonData = JsonSerializer.Deserialize<JsonElement>(jsonText);
        }

        public void Reload()
        {
            LoadJson();
        }

        public string Get(string path)
        {
            var parts = path.Split(':');
            JsonElement current = _jsonData;

            foreach (var part in parts)
            {
                if (current.ValueKind == JsonValueKind.Object)
                {
                    if (!current.TryGetProperty(part, out var next))
                        return "";
                    current = next;
                }
                else if (current.ValueKind == JsonValueKind.Array)
                {
                    if (!int.TryParse(part, out int index))
                        return "";
                    if (index < 0 || index >= current.GetArrayLength())
                        return "";
                    current = current[index];
                }
                else
                {
                    return "";
                }
            }

            return current.GetString() ?? "";
        }
    }
}
