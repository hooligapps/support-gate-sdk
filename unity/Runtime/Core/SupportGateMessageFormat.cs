using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hooligapps.SupportGate
{
    /// <summary>
    /// Подстановка в строки ICU MessageFormat — ровно то подмножество, которое
    /// используют каталоги шлюза: `{name}`, `{n, plural, one {…} other {…}}` с `=N`
    /// и `#`, `{x, select, a {…} other {…}}`. Вложенность разрешена. Полноценной
    /// библиотеки в пакете нет: строк три десятка.
    ///
    /// Категории множественного числа считаются по CLDR для языков, которые отдаёт
    /// шлюз; для незнакомого языка — one/other по английским правилам.
    /// </summary>
    public static class SupportGateMessageFormat
    {
        private sealed class Argument
        {
            public string Name;
            public string Kind; // value | plural | select
            public Dictionary<string, List<object>> Options;
        }

        private static readonly Dictionary<string, List<object>> Cache = new Dictionary<string, List<object>>();

        public static string Format(string message, IDictionary<string, object> values, string locale)
        {
            if (string.IsNullOrEmpty(message))
            {
                return message ?? string.Empty;
            }

            List<object> nodes;
            lock (Cache)
            {
                if (!Cache.TryGetValue(message, out nodes))
                {
                    try
                    {
                        nodes = Parse(message);
                    }
                    catch (FormatException)
                    {
                        // Кривая строка в каталоге не должна ломать форму: показываем как есть.
                        nodes = new List<object> { message };
                    }

                    Cache[message] = nodes;
                }
            }

            var output = new StringBuilder();
            Render(nodes, values ?? new Dictionary<string, object>(), locale ?? "en", null, output);
            return output.ToString();
        }

        /// <summary>Категория CLDR для числа: one, two, few, many, other или zero.</summary>
        public static string PluralCategory(double n, string locale)
        {
            var language = (locale ?? "en").Split('-', '_')[0].ToLowerInvariant();
            var isInteger = Math.Abs(n - Math.Round(n)) < double.Epsilon;
            var i = (long)Math.Abs(Math.Round(n));
            var mod10 = i % 10;
            var mod100 = i % 100;

            switch (language)
            {
                case "ja":
                case "ko":
                case "zh":
                case "th":
                case "vi":
                case "id":
                    return "other";

                case "fr":
                case "pt":
                    // fr и pt-br: 0 и 1 — one; pt-pt в CLDR ведёт себя как pt — расхождение
                    // лишь в диапазоне, которого в каталоге нет.
                    return i == 0 || i == 1 ? "one" : "other";

                case "ru":
                case "uk":
                    if (!isInteger) return "other";
                    if (mod10 == 1 && mod100 != 11) return "one";
                    if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return "few";
                    return "many";

                case "pl":
                    if (!isInteger) return "other";
                    if (i == 1) return "one";
                    if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return "few";
                    return "many";

                case "cs":
                    if (!isInteger) return "many";
                    if (i == 1) return "one";
                    if (i >= 2 && i <= 4) return "few";
                    return "other";

                case "ro":
                    if (!isInteger) return "few";
                    if (i == 1) return "one";
                    if (i == 0 || (mod100 >= 2 && mod100 <= 19)) return "few";
                    return "other";

                case "ar":
                    if (!isInteger) return "other";
                    if (i == 0) return "zero";
                    if (i == 1) return "one";
                    if (i == 2) return "two";
                    if (mod100 >= 3 && mod100 <= 10) return "few";
                    if (mod100 >= 11 && mod100 <= 99) return "many";
                    return "other";

                default:
                    // en, de, nl, sv, da, no, fi, hu, el, it, es, bg, tr и остальные.
                    return isInteger && i == 1 ? "one" : "other";
            }
        }

        private static void Render(List<object> nodes, IDictionary<string, object> values, string locale,
            string hash, StringBuilder output)
        {
            foreach (var node in nodes)
            {
                if (node is string literal)
                {
                    output.Append(hash == null ? literal : literal.Replace("#", hash));
                    continue;
                }

                var argument = (Argument)node;
                values.TryGetValue(argument.Name, out var value);

                if (argument.Kind == "value")
                {
                    output.Append(value == null
                        ? "{" + argument.Name + "}"
                        : Convert.ToString(value, CultureInfo.InvariantCulture));
                    continue;
                }

                if (argument.Kind == "plural")
                {
                    var number = ToNumber(value);
                    var text = Convert.ToString(value ?? string.Empty, CultureInfo.InvariantCulture);
                    var branch = Pick(argument.Options,
                        "=" + Convert.ToString(number, CultureInfo.InvariantCulture),
                        PluralCategory(number, locale));
                    if (branch != null)
                    {
                        Render(branch, values, locale, text, output);
                    }

                    continue;
                }

                var selected = Pick(argument.Options,
                    Convert.ToString(value ?? string.Empty, CultureInfo.InvariantCulture), null);
                if (selected != null)
                {
                    Render(selected, values, locale, hash, output);
                }
            }
        }

        private static List<object> Pick(Dictionary<string, List<object>> options, string first, string second)
        {
            List<object> branch;
            if (first != null && options.TryGetValue(first, out branch)) return branch;
            if (second != null && options.TryGetValue(second, out branch)) return branch;
            return options.TryGetValue("other", out branch) ? branch : null;
        }

        private static double ToNumber(object value)
        {
            if (value == null) return double.NaN;
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is double d) return d;
            if (value is float f) return f;
            double parsed;
            return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float,
                CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : double.NaN;
        }

        private sealed class State
        {
            public string Text;
            public int Pos;
        }

        private static List<object> Parse(string message)
        {
            var state = new State { Text = message };
            var nodes = ParseNodes(state, false);
            if (state.Pos != message.Length)
            {
                throw new FormatException("unbalanced braces");
            }

            return nodes;
        }

        private static List<object> ParseNodes(State state, bool nested)
        {
            var nodes = new List<object>();
            var literal = new StringBuilder();

            while (state.Pos < state.Text.Length)
            {
                var c = state.Text[state.Pos];
                if (c == '}' && nested) break;
                if (c != '{')
                {
                    literal.Append(c);
                    state.Pos++;
                    continue;
                }

                if (literal.Length > 0)
                {
                    nodes.Add(literal.ToString());
                    literal.Length = 0;
                }

                state.Pos++;
                nodes.Add(ParseArgument(state));
            }

            if (literal.Length > 0)
            {
                nodes.Add(literal.ToString());
            }

            return nodes;
        }

        private static Argument ParseArgument(State state)
        {
            var end = FindArgumentEnd(state);
            var head = state.Text.Substring(state.Pos, end - state.Pos);
            var parts = head.Split(',');
            var name = parts[0].Trim();
            if (name.Length == 0)
            {
                throw new FormatException("empty argument");
            }

            if (parts.Length == 1)
            {
                state.Pos = end + 1;
                return new Argument { Name = name, Kind = "value", Options = new Dictionary<string, List<object>>() };
            }

            var kind = parts[1].Trim();
            if (kind != "plural" && kind != "select")
            {
                throw new FormatException("unsupported type " + kind);
            }

            state.Pos = end;
            var options = new Dictionary<string, List<object>>();
            while (true)
            {
                SkipSpaces(state);
                if (state.Pos >= state.Text.Length)
                {
                    throw new FormatException("unterminated argument");
                }

                if (state.Text[state.Pos] == '}')
                {
                    state.Pos++;
                    break;
                }

                var keyStart = state.Pos;
                while (state.Pos < state.Text.Length && !char.IsWhiteSpace(state.Text[state.Pos]) &&
                       state.Text[state.Pos] != '{')
                {
                    state.Pos++;
                }

                var key = state.Text.Substring(keyStart, state.Pos - keyStart);
                SkipSpaces(state);
                if (key.Length == 0 || state.Pos >= state.Text.Length || state.Text[state.Pos] != '{')
                {
                    throw new FormatException("bad option");
                }

                state.Pos++;
                options[key] = ParseNodes(state, true);
                if (state.Pos >= state.Text.Length || state.Text[state.Pos] != '}')
                {
                    throw new FormatException("unclosed option");
                }

                state.Pos++;
            }

            if (!options.ContainsKey("other"))
            {
                throw new FormatException("missing other");
            }

            return new Argument { Name = name, Kind = kind, Options = options };
        }

        /// <summary>Конец заголовка аргумента: `}` у простого, второй `,` у plural/select.</summary>
        private static int FindArgumentEnd(State state)
        {
            var commas = 0;
            for (var i = state.Pos; i < state.Text.Length; i++)
            {
                var c = state.Text[i];
                if (c == '}') return i;
                if (c == '{') throw new FormatException("unexpected brace");
                if (c == ',' && ++commas == 2) return i + 1;
            }

            throw new FormatException("unterminated argument");
        }

        private static void SkipSpaces(State state)
        {
            while (state.Pos < state.Text.Length && char.IsWhiteSpace(state.Text[state.Pos]))
            {
                state.Pos++;
            }
        }
    }
}
