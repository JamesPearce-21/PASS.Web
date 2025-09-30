using System.IO;
using System.Text.Json;
using PASS.Web.Models;

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

            return current.GetString();
        }

        public SectionWrapper GetWrapper(string sectionKey)
        {
            try
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "content", "content.json");
                if (!System.IO.File.Exists(path)) return new SectionWrapper();

                var jsonText = System.IO.File.ReadAllText(path);
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText) ?? new();

                if (!dict.ContainsKey(sectionKey)) return new SectionWrapper();

                var wrapper = JsonSerializer.Deserialize<SectionWrapper>(dict[sectionKey].GetRawText());
                return wrapper ?? new SectionWrapper();
            }
            catch
            {
                return new SectionWrapper();
            }
        }

    }
}
