using System.Text;

namespace SteamSwitcher.Core.Vdf;

public static class VdfSerializer
{
    public static string Serialize(VdfObject root)
    {
        var sb = new StringBuilder();
        foreach (var child in root.Children)
        {
            WriteNode(sb, child, 0);
        }
        return sb.ToString();
    }

    private static void WriteNode(StringBuilder sb, VdfNode node, int indentLevel)
    {
        var indent = new string('\t', indentLevel);

        if (node is VdfValue val)
        {
            sb.Append(indent);
            sb.Append('"').Append(Escape(val.Key)).Append("\"\t\t\"");
            sb.Append(Escape(val.Value)).Append('"');
            sb.AppendLine();
        }
        else if (node is VdfObject obj)
        {
            sb.Append(indent);
            sb.Append('"').Append(Escape(obj.Key)).Append('"');
            sb.AppendLine();
            sb.Append(indent).AppendLine("{");

            foreach (var child in obj.Children)
            {
                WriteNode(sb, child, indentLevel + 1);
            }

            sb.Append(indent).AppendLine("}");
        }
    }

    private static string Escape(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }
}
