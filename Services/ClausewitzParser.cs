using System.Text;

namespace EU4SaveAnalyzer.Services;

/// <summary>
/// Parser für das Clausewitz-Textformat das von Paradox-Spielen (EU4, CK3, HOI4) verwendet wird.
/// Das Format ist ein verschachteltes Key-Value-Format:
///   key=value
///   key={ nested=value ... }
/// </summary>
public class ClausewitzParser
{
    private readonly string _text;
    private int _pos;

    public ClausewitzParser(string text)
    {
        _text = text;
        _pos = 0;
    }

    /// <summary>
    /// Parst den gesamten Text und gibt ein Dictionary mit allen geparsten Werten zurück.
    /// Werte können strings, Listen (List&lt;object&gt;) oder weitere Dictionaries sein.
    /// </summary>
    public Dictionary<string, object> Parse()
    {
        return ParseBlock();
    }

    // Liest einen gesamten Block (zwischen { }) oder das Root-Dokument
    private Dictionary<string, object> ParseBlock()
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        while (_pos < _text.Length)
        {
            SkipWhitespaceAndComments();
            if (_pos >= _text.Length) break;

            // End of block
            if (_text[_pos] == '}')
            {
                _pos++;
                break;
            }

            // Liest Key
            string key = ReadToken();
            if (string.IsNullOrEmpty(key)) break;

            SkipWhitespaceAndComments();

            // Prüft ob '=' folgt
            if (_pos < _text.Length && _text[_pos] == '=')
            {
                _pos++; // '=' überspringen
                SkipWhitespaceAndComments();

                object value = ReadValue();

                // Bei doppelten Keys wird ein List-Eintrag erzeugt
                if (result.ContainsKey(key))
                {
                    if (result[key] is List<object> list)
                        list.Add(value);
                    else
                        result[key] = new List<object> { result[key], value };
                }
                else
                {
                    result[key] = value;
                }
            }
        }

        return result;
    }

    // Liest einen Wert: entweder einen Block {}, einen String, oder ein Token
    private object ReadValue()
    {
        if (_pos >= _text.Length) return string.Empty;

        if (_text[_pos] == '{')
        {
            _pos++; // '{' überspringen
            return ParseBlock();
        }
        if (_text[_pos] == '"')
        {
            return ReadQuotedString();
        }
        return ReadToken();
    }

    // Liest einen in Anführungszeichen eingeschlossenen String
    private string ReadQuotedString()
    {
        _pos++; // öffnendes " überspringen
        var sb = new StringBuilder();
        while (_pos < _text.Length && _text[_pos] != '"')
        {
            if (_text[_pos] == '\\' && _pos + 1 < _text.Length)
            {
                _pos++;
                sb.Append(_text[_pos]);
            }
            else
            {
                sb.Append(_text[_pos]);
            }
            _pos++;
        }
        if (_pos < _text.Length) _pos++; // schließendes " überspringen
        return sb.ToString();
    }

    // Liest ein Token (alphanumerisch, Punkte, Minuszeichen für Zahlen und Daten)
    private string ReadToken()
    {
        var sb = new StringBuilder();
        while (_pos < _text.Length &&
               !char.IsWhiteSpace(_text[_pos]) &&
               _text[_pos] != '=' &&
               _text[_pos] != '{' &&
               _text[_pos] != '}' &&
               _text[_pos] != '#')
        {
            sb.Append(_text[_pos]);
            _pos++;
        }
        return sb.ToString();
    }

    // Überspringt Leerzeichen und Kommentare (#...)
    private void SkipWhitespaceAndComments()
    {
        while (_pos < _text.Length)
        {
            if (char.IsWhiteSpace(_text[_pos]))
            {
                _pos++;
            }
            else if (_text[_pos] == '#')
            {
                // Kommentar bis Zeilenende überspringen
                while (_pos < _text.Length && _text[_pos] != '\n')
                    _pos++;
            }
            else
            {
                break;
            }
        }
    }

    // ---- Hilfsmethoden für das Auslesen von Werten aus dem Dictionary ----

    /// <summary>Liest einen String-Wert aus dem Dictionary, mit Fallback.</summary>
    public static string GetString(Dictionary<string, object> dict, string key, string fallback = "")
    {
        if (dict.TryGetValue(key, out var val))
            return val?.ToString()?.Trim('"') ?? fallback;
        return fallback;
    }

    /// <summary>Liest einen Double-Wert aus dem Dictionary, mit Fallback 0.</summary>
    public static double GetDouble(Dictionary<string, object> dict, string key, double fallback = 0)
    {
        if (dict.TryGetValue(key, out var val))
        {
            var s = val?.ToString()?.Trim('"') ?? "";
            if (double.TryParse(s, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double d))
                return d;
        }
        return fallback;
    }

    /// <summary>Liest einen Int-Wert aus dem Dictionary, mit Fallback 0.</summary>
    public static int GetInt(Dictionary<string, object> dict, string key, int fallback = 0)
    {
        return (int)GetDouble(dict, key, fallback);
    }

    /// <summary>Liest ein Sub-Dictionary aus dem Dictionary.</summary>
    public static Dictionary<string, object>? GetDict(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out var val) && val is Dictionary<string, object> d)
            return d;
        return null;
    }

    /// <summary>
    /// Summiert alle numerischen Werte eines Dictionaries (für indexed arrays wie adm_spent_indexed).
    /// </summary>
    public static int SumDictValues(Dictionary<string, object>? dict)
    {
        if (dict == null) return 0;
        int sum = 0;
        foreach (var kv in dict)
        {
            if (int.TryParse(kv.Value?.ToString(), out int v))
                sum += v;
        }
        return sum;
    }

    /// <summary>
    /// Liest bestimmte Indizes aus einem indexed-Dict und summiert sie.
    /// EU4 speichert Mana-Ausgaben als indexed arrays: 0=Tech, 1=Ideas, usw.
    /// </summary>
    public static int SumIndices(Dictionary<string, object>? dict, params int[] indices)
    {
        if (dict == null) return 0;
        int sum = 0;
        foreach (int idx in indices)
        {
            var key = idx.ToString();
            if (dict.TryGetValue(key, out var val) &&
                int.TryParse(val?.ToString(), out int v))
                sum += v;
        }
        return sum;
    }
}
