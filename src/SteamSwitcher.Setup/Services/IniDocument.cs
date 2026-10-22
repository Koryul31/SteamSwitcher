using System.IO;
using System.Text;

namespace SteamSwitcher.Setup.Services;

public sealed class IniDocument
{
    public Dictionary<string, Dictionary<string, string>> Sections { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public List<string> SectionOrder { get; } = [];

    public static IniDocument Load(string path)
    {
        var document = new IniDocument();
        if (!File.Exists(path))
        {
            return document;
        }

        var current = "General";
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = line[1..^1].Trim();
                if (!document.Sections.ContainsKey(current))
                {
                    document.Sections[current] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    document.SectionOrder.Add(current);
                }
                continue;
            }

            var equals = line.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            var key = line[..equals].Trim();
            var value = line[(equals + 1)..].Trim();
            if (!document.Sections.TryGetValue(current, out var values))
            {
                values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                document.Sections[current] = values;
                document.SectionOrder.Add(current);
            }

            values[key] = value;
        }

        return document;
    }

    public string? Get(string section, string key)
    {
        return Sections.TryGetValue(section, out var values) && values.TryGetValue(key, out var value)
            ? value
            : null;
    }

    public void Set(string section, string key, string value)
    {
        if (!Sections.TryGetValue(section, out var values))
        {
            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Sections[section] = values;
            SectionOrder.Add(section);
        }

        values[key] = value;
    }

    public void RemoveSections(Func<string, bool> predicate)
    {
        foreach (var section in Sections.Keys.Where(predicate).ToList())
        {
            Sections.Remove(section);
        }

        SectionOrder.RemoveAll(section => predicate(section));
    }

    public void Save(string path)
    {
        var builder = new StringBuilder();
        foreach (var sectionName in SectionOrder)
        {
            if (!Sections.TryGetValue(sectionName, out var values))
            {
                continue;
            }

            builder.Append('[').Append(sectionName).Append(']').AppendLine();
            foreach (var value in values)
            {
                builder.Append(value.Key).Append('=').AppendLine(value.Value);
            }
            builder.AppendLine();
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
    }
}
