using System.Text;

namespace EU4SaveAnalyzer.Services;

/// <summary>
/// Parser für das Clausewitz-Textformat das von Paradox-Spielen (EU4, CK3, HOI4) verwendet wird.
/// Das Format ist ein verschachteltes Key-Value-Format:
///   key=value
///   key={ nested=value ... }
///
/// Besonderheit: Manche Blöcke enthalten nur Werte ohne Keys (Listen), z.B.:
///   players_countries={ "SteamName" "FRA" "OtherPlayer" "ENG" }
/// Diese werden unter dem internen Schlüssel "_values" als List<string> gespeichert.
/// </summary>
public class ClausewitzParser
{
    private readonly string _text;
    private int _pos;

    /// <summary>Erstellt einen neuen Parser für den gegebenen Rohtext.</summary>
    public ClausewitzParser(string text)
    {
        _text = text;
        _pos  = 0;
    }

    /// <summary>
    /// Parst den gesamten Text und gibt ein Dictionary mit allen geparsten Werten zurück.
    /// </summary>
    public Dictionary<string, object> Parse()
    {
        return ParseBlock(isRoot: true);
    }

    /// <summary>
    /// Liest einen gesamten Block (zwischen { }) oder das Root-Dokument.
    /// isRoot=true: } wird nicht als Blockende behandelt (Root hat keine schließende Klammer).
    /// </summary>
    private Dictionary<string, object> ParseBlock(bool isRoot = false)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        while (_pos < _text.Length)
        {
            SkipWhitespaceAndComments();
            if (_pos >= _text.Length) break;

            // Blockende: } schließt einen Sub-Block
            if (!isRoot && _text[_pos] == '}')
            {
                _pos++;
                break;
            }

            // Verirrtes } im Root überspringen
            if (isRoot && _text[_pos] == '}')
            {
                _pos++;
                continue;
            }

            // Key lesen: quoted String oder normales Token
            string key;
            if (_text[_pos] == '"')
                key = ReadQuotedString();
            else
                key = ReadToken();

            // Leeres Token: unbekanntes Zeichen überspringen (verhindert Endlosschleife)
            if (string.IsNullOrEmpty(key))
            {
                _pos++;
                continue;
            }

            SkipWhitespaceAndComments();

            if (_pos < _text.Length && _text[_pos] == '=')
            {
                // Normaler Key=Value Eintrag
                _pos++;
                SkipWhitespaceAndComments();

                object value = ReadValue();

                // Doppelte Keys → als Liste behandeln (z.B. active_war, previous_war)
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
            else
            {
                // Bare Value (kein '=') → unter "_values" sammeln
                if (!result.ContainsKey("_values"))
                    result["_values"] = new List<string>();

                ((List<string>)result["_values"]).Add(key.Trim('"'));
            }
        }

        return result;
    }

    /// <summary>Liest einen Wert: Block {}, Quoted String oder Token.</summary>
    private object ReadValue()
    {
        if (_pos >= _text.Length) return string.Empty;

        if (_text[_pos] == '{')
        {
            _pos++;
            return ParseBlock(isRoot: false);
        }
        if (_text[_pos] == '"')
            return ReadQuotedString();

        return ReadToken();
    }

    /// <summary>Liest einen in Anführungszeichen eingeschlossenen String.</summary>
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

    /// <summary>
    /// Liest ein Token bis zum nächsten Trennzeichen (Whitespace, =, {, }, #, ").
    /// </summary>
    private string ReadToken()
    {
        var sb = new StringBuilder();
        while (_pos < _text.Length &&
               !char.IsWhiteSpace(_text[_pos]) &&
               _text[_pos] != '=' &&
               _text[_pos] != '{' &&
               _text[_pos] != '}' &&
               _text[_pos] != '#' &&
               _text[_pos] != '"')
        {
            sb.Append(_text[_pos]);
            _pos++;
        }
        return sb.ToString();
    }

    /// <summary>Überspringt Leerzeichen, Tabs, Zeilenumbrüche und # Kommentare.</summary>
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
                while (_pos < _text.Length && _text[_pos] != '\n')
                    _pos++;
            }
            else
            {
                break;
            }
        }
    }

    // ============================================================
    // Statische Hilfsmethoden
    // ============================================================

    /// <summary>Liest einen String-Wert aus dem Dictionary, mit Fallback.</summary>
    public static string GetString(Dictionary<string, object> dict, string key, string fallback = "")
    {
        if (dict.TryGetValue(key, out var val))
            return val?.ToString()?.Trim('"') ?? fallback;
        return fallback;
    }

    /// <summary>Liest einen Double-Wert (InvariantCulture), mit Fallback 0.</summary>
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
        return (int)Math.Round(GetDouble(dict, key, fallback));
    }

    /// <summary>
    /// Gibt ein verschachteltes Sub-Dictionary zurück.
    /// Bei List-Wert (doppelter Key) wird das erste Dictionary-Element genommen.
    /// </summary>
    public static Dictionary<string, object>? GetDict(Dictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var val)) return null;

        if (val is Dictionary<string, object> d) return d;

        if (val is List<object> list)
            return list.OfType<Dictionary<string, object>>().FirstOrDefault();

        return null;
    }

    /// <summary>
    /// Gibt bare-value Strings zurück die unter "_values" gespeichert wurden.
    /// Für Blöcke wie players_countries={ "Name" "TAG" ... }.
    /// </summary>
    public static List<string> GetBareValues(Dictionary<string, object> dict, string key)
    {
        var inner = GetDict(dict, key);
        if (inner == null) return new List<string>();

        if (inner.TryGetValue("_values", out var listObj) && listObj is List<string> list)
            return list;

        return new List<string>();
    }

    /// <summary>
    /// Summiert alle numerischen Int-Werte eines Dictionaries.
    /// Für indexed arrays (z.B. adm_spent_indexed={ 0=500 1=1000 }).
    /// </summary>
    public static int SumDictValues(Dictionary<string, object>? dict)
    {
        if (dict == null) return 0;
        int sum = 0;
        foreach (var kv in dict)
        {
            if (kv.Key == "_values") continue;
            if (int.TryParse(kv.Value?.ToString(), out int v))
                sum += v;
        }
        return sum;
    }

    /// <summary>
    /// Summiert alle numerischen Double-Werte eines Dictionaries.
    /// Für ledger-Ausgaben wo Dezimalwerte vorkommen.
    /// </summary>
    public static double SumDictIndexedDoubles(Dictionary<string, object>? dict)
    {
        if (dict == null) return 0;
        double sum = 0;
        foreach (var kv in dict)
        {
            if (kv.Key == "_values") continue;
            if (double.TryParse(kv.Value?.ToString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double v))
                sum += v;
        }
        return sum;
    }

    /// <summary>
    /// Summiert bestimmte Indizes aus einem indexed-Dict.
    /// EU4 speichert Mana-Ausgaben als: adm_spent_indexed={ 0=500 1=1000 2=200 }
    /// </summary>
    public static int SumIndices(Dictionary<string, object>? dict, params int[] indices)
    {
        if (dict == null) return 0;
        int sum = 0;
        foreach (int idx in indices)
        {
            if (dict.TryGetValue(idx.ToString(), out var val) &&
                int.TryParse(val?.ToString(), out int v))
                sum += v;
        }
        return sum;
    }

    /// <summary>
    /// Liest einen Double-Wert anhand eines numerischen Index aus einem indexed-Dict.
    /// Für ledger.lastmonthincome und lastmonthexpense.
    /// </summary>
    public static double GetIndexedDouble(Dictionary<string, object>? dict, int index)
    {
        if (dict == null) return 0;
        if (dict.TryGetValue(index.ToString(), out var val))
        {
            if (double.TryParse(val?.ToString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double d))
                return d;
        }
        return 0;
    }
}
