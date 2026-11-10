using System.IO;
using System.Text.Json;

namespace DocumentGenerator.Services;

/// <summary>
/// Зберігає значення змінних, відображувані назви та порядок полів для кожного шаблону.
/// </summary>
public sealed class VariableStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string StoreDir => Path.Combine(Infrastructure.AppPaths.Data, "variables");
    private static string LabelsDir => Path.Combine(Infrastructure.AppPaths.Data, "labels");
    private static string OrderDir => Path.Combine(Infrastructure.AppPaths.Data, "order");

    public Dictionary<string, string> LoadValues(int templateId)
        => LoadDict(Path.Combine(StoreDir, $"template_{templateId}.json"));

    public void SaveValues(int templateId, IReadOnlyDictionary<string, string> values)
        => SaveDict(Path.Combine(StoreDir, $"template_{templateId}.json"), values);

    public Dictionary<string, string> LoadLabels(int templateId)
        => LoadDict(Path.Combine(LabelsDir, $"template_{templateId}.json"));

    public void SaveLabels(int templateId, IReadOnlyDictionary<string, string> labels)
        => SaveDict(Path.Combine(LabelsDir, $"template_{templateId}.json"), labels);

    public List<string> LoadOrder(int templateId)
    {
        try
        {
            var path = Path.Combine(OrderDir, $"template_{templateId}.json");
            if (!File.Exists(path)) return new List<string>();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public void SaveOrder(int templateId, IReadOnlyList<string> order)
    {
        try
        {
            Directory.CreateDirectory(OrderDir);
            var path = Path.Combine(OrderDir, $"template_{templateId}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(order, JsonOptions));
        }
        catch { /* ignore */ }
    }

    private static Dictionary<string, string> LoadDict(string path)
    {
        try
        {
            if (!File.Exists(path)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
            return data is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void SaveDict(string path, IReadOnlyDictionary<string, string> data)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions));
        }
        catch { /* ignore */ }
    }
}
