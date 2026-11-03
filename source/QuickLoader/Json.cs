using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FIFA19QuickLoader
{
    /// <summary>JSON object that keeps key order so FMM's config round-trips without reshuffling.</summary>
    internal sealed class JsonObject
    {
        private readonly List<string> keys = new List<string>();
        private readonly Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);

        public IEnumerable<string> Keys => keys;

        public bool Contains(string key) => values.ContainsKey(key);

        public object this[string key]
        {
            get { values.TryGetValue(key, out var v); return v; }
            set
            {
                if (!values.ContainsKey(key)) keys.Add(key);
                values[key] = value;
            }
        }

        public bool Remove(string key)
        {
            if (!values.Remove(key)) return false;
            keys.Remove(key);
            return true;
        }

        public JsonObject GetObject(string key) => this[key] as JsonObject;

        public string GetString(string key) => this[key] as string;

        public bool? GetBool(string key) => this[key] as bool?;

        public JsonObject GetOrCreateObject(string key)
        {
            var o = GetObject(key);
            if (o == null)
            {
                o = new JsonObject();
                this[key] = o;
            }
            return o;
        }
    }

    /// <summary>Keeps the literal text of numbers so values are written back unchanged.</summary>
    internal sealed class JsonNumber
    {
        public JsonNumber(string raw) { Raw = raw; }
        public string Raw { get; }
        public override string ToString() => Raw;
    }

    internal static class Json
    {
        public static object Parse(string text)
        {
            var p = new Parser(text);
            p.SkipWs();
            var v = p.ReadValue();
            p.SkipWs();
            if (!p.End) throw p.Error("JSON 末尾有多余内容");
            return v;
        }

        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, 0);
            sb.Append("\r\n");
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value, int indent)
        {
            switch (value)
            {
                case null: sb.Append("null"); break;
                case string s: WriteString(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case JsonNumber n: sb.Append(n.Raw); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case JsonObject o:
                    {
                        var first = true;
                        sb.Append('{');
                        foreach (var k in o.Keys)
                        {
                            sb.Append(first ? "\r\n" : ",\r\n");
                            first = false;
                            sb.Append(' ', (indent + 1) * 2);
                            WriteString(sb, k);
                            sb.Append(": ");
                            WriteValue(sb, o[k], indent + 1);
                        }
                        if (!first) { sb.Append("\r\n").Append(' ', indent * 2); }
                        sb.Append('}');
                        break;
                    }
                case List<object> list:
                    {
                        sb.Append('[');
                        for (int i = 0; i < list.Count; i++)
                        {
                            sb.Append(i == 0 ? "\r\n" : ",\r\n");
                            sb.Append(' ', (indent + 1) * 2);
                            WriteValue(sb, list[i], indent + 1);
                        }
                        if (list.Count > 0) { sb.Append("\r\n").Append(' ', indent * 2); }
                        sb.Append(']');
                        break;
                    }
                default: throw new InvalidOperationException("不支持的 JSON 值类型: " + value.GetType());
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private sealed class Parser
        {
            private readonly string s;
            private int pos;

            public Parser(string text)
            {
                s = text ?? throw new ArgumentNullException(nameof(text));
                if (s.Length > 0 && s[0] == '\uFEFF') pos = 1;
            }

            public bool End => pos >= s.Length;

            public FormatException Error(string msg) => new FormatException($"{msg}（位置 {pos}）");

            public void SkipWs()
            {
                while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
            }

            private char Peek() => pos < s.Length ? s[pos] : '\0';

            private void Expect(char c)
            {
                if (Peek() != c) throw Error($"应为 '{c}'");
                pos++;
            }

            public object ReadValue()
            {
                SkipWs();
                var c = Peek();
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': ReadLiteral("true"); return true;
                    case 'f': ReadLiteral("false"); return false;
                    case 'n': ReadLiteral("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Error("无法识别的 JSON 值");
                }
            }

            private void ReadLiteral(string lit)
            {
                if (string.CompareOrdinal(s, pos, lit, 0, lit.Length) != 0) throw Error("无效的字面量");
                pos += lit.Length;
            }

            private JsonNumber ReadNumber()
            {
                int start = pos;
                while (pos < s.Length && "+-0123456789.eE".IndexOf(s[pos]) >= 0) pos++;
                var raw = s.Substring(start, pos - start);
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) throw Error("无效数字");
                return new JsonNumber(raw);
            }

            private JsonObject ReadObject()
            {
                Expect('{');
                var o = new JsonObject();
                SkipWs();
                if (Peek() == '}') { pos++; return o; }
                while (true)
                {
                    SkipWs();
                    var key = ReadString();
                    SkipWs();
                    Expect(':');
                    o[key] = ReadValue();
                    SkipWs();
                    if (Peek() == ',') { pos++; continue; }
                    Expect('}');
                    return o;
                }
            }

            private List<object> ReadArray()
            {
                Expect('[');
                var list = new List<object>();
                SkipWs();
                if (Peek() == ']') { pos++; return list; }
                while (true)
                {
                    list.Add(ReadValue());
                    SkipWs();
                    if (Peek() == ',') { pos++; continue; }
                    Expect(']');
                    return list;
                }
            }

            private string ReadString()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (true)
                {
                    if (pos >= s.Length) throw Error("字符串未结束");
                    var c = s[pos++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (pos >= s.Length) throw Error("转义未结束");
                    var e = s[pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (pos + 4 > s.Length) throw Error("\\u 转义不完整");
                            sb.Append((char)int.Parse(s.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            pos += 4;
                            break;
                        default: throw Error("无效转义");
                    }
                }
            }
        }
    }
}
