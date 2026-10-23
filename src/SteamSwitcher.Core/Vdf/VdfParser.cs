using System.Text;

namespace SteamSwitcher.Core.Vdf;

public static class VdfParser
{
    public static VdfObject Parse(string text)
    {
        var tokens = Tokenize(text);
        var root = new VdfObject("root");
        var index = 0;

        while (index < tokens.Count)
        {
            var node = ParseNode(tokens, ref index);
            if (node != null)
            {
                root.Children.Add(node);
            }
        }

        return root;
    }

    private static VdfNode? ParseNode(List<string> tokens, ref int index)
    {
        if (index >= tokens.Count)
            return null;

        var key = tokens[index++];
        if (key == "}" || key == "{")
            return null;

        if (index >= tokens.Count)
            return new VdfValue(key, string.Empty);

        var next = tokens[index];
        if (next == "{")
        {
            index++; // consume "{"
            var obj = new VdfObject(key);
            while (index < tokens.Count && tokens[index] != "}")
            {
                var child = ParseNode(tokens, ref index);
                if (child != null)
                {
                    obj.Children.Add(child);
                }
            }

            if (index < tokens.Count && tokens[index] == "}")
            {
                index++; // consume "}"
            }

            return obj;
        }
        else
        {
            index++; // consume value token
            return new VdfValue(key, next);
        }
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var len = text.Length;
        var i = 0;

        while (i < len)
        {
            var c = text[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            // Comment line
            if (c == '/' && i + 1 < len && text[i + 1] == '/')
            {
                i += 2;
                while (i < len && text[i] != '\n' && text[i] != '\r')
                {
                    i++;
                }
                continue;
            }

            if (c == '{' || c == '}')
            {
                tokens.Add(c.ToString());
                i++;
                continue;
            }

            // Quoted string
            if (c == '"')
            {
                i++;
                var sb = new StringBuilder();
                while (i < len)
                {
                    var ch = text[i];
                    if (ch == '\\' && i + 1 < len)
                    {
                        var nextCh = text[i + 1];
                        if (nextCh == '"' || nextCh == '\\')
                        {
                            sb.Append(nextCh);
                            i += 2;
                            continue;
                        }
                        if (nextCh == 'n')
                        {
                            sb.Append('\n');
                            i += 2;
                            continue;
                        }
                        if (nextCh == 't')
                        {
                            sb.Append('\t');
                            i += 2;
                            continue;
                        }
                    }

                    if (ch == '"')
                    {
                        i++;
                        break;
                    }

                    sb.Append(ch);
                    i++;
                }

                tokens.Add(sb.ToString());
                continue;
            }

            // Bare word token
            var start = i;
            while (i < len && !char.IsWhiteSpace(text[i]) && text[i] != '{' && text[i] != '}' && text[i] != '"')
            {
                i++;
            }
            tokens.Add(text[start..i]);
        }

        return tokens;
    }
}
