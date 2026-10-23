namespace SteamSwitcher.Core.Vdf;

public abstract class VdfNode
{
    public string Key { get; set; } = string.Empty;
}

public sealed class VdfValue : VdfNode
{
    public string Value { get; set; } = string.Empty;

    public VdfValue() { }

    public VdfValue(string key, string value)
    {
        Key = key;
        Value = value;
    }

    public override string ToString() => $"\"{Key}\" \"{Value}\"";
}

public sealed class VdfObject : VdfNode
{
    public List<VdfNode> Children { get; } = [];

    public VdfObject() { }

    public VdfObject(string key)
    {
        Key = key;
    }

    public VdfNode? this[string key] =>
        Children.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

    public string? GetString(string key) =>
        (this[key] as VdfValue)?.Value;

    public VdfObject? GetObject(string key) =>
        this[key] as VdfObject;

    public void SetValue(string key, string value)
    {
        var existing = Children.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));
        if (existing is VdfValue vdfVal)
        {
            vdfVal.Value = value;
        }
        else
        {
            if (existing != null) Children.Remove(existing);
            Children.Add(new VdfValue(key, value));
        }
    }

    public void SetObject(string key, VdfObject obj)
    {
        obj.Key = key;
        var existingIndex = Children.FindIndex(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            Children[existingIndex] = obj;
        }
        else
        {
            Children.Add(obj);
        }
    }
}
