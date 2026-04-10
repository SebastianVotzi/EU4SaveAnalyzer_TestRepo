using Microsoft.AspNetCore.Mvc;
using EU4SaveAnalyzer.Services;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace EU4SaveAnalyzer.Controllers;

public class DiagnosticController : Controller
{
    [HttpPost]
    public async Task<IActionResult> Inspect(IFormFile? saveFile)
    {
        if (saveFile == null) return Json(new { error = "Keine Datei" });
        var ms = new MemoryStream();
        await saveFile.OpenReadStream().CopyToAsync(ms);
        ms.Position = 0;
        byte[] hdr = new byte[8]; ms.Read(hdr, 0, 8); ms.Position = 0;
        string rawText;
        if (hdr[0] == 0x50 && hdr[1] == 0x4B)
        {
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read, true);
            var entry = zip.GetEntry("gamestate") ?? zip.Entries.First();
            using var sr = new StreamReader(entry.Open(), Encoding.Latin1);
            rawText = await sr.ReadToEndAsync();
        }
        else { ms.Position = 0; using var sr = new StreamReader(ms, Encoding.Latin1); rawText = await sr.ReadToEndAsync(); }
        if (rawText.StartsWith("EU4txt", StringComparison.OrdinalIgnoreCase))
        { int nl = rawText.IndexOf('\n'); if (nl >= 0) rawText = rawText[(nl + 1)..]; }

        var result = new Dictionary<string, object>();

        // 1. Suche nach DAN-Block im Rohtext
        // EU4 schreibt: "\nDAN={\n" oder "DAN={\r\n"
        int danPos = -1;
        foreach (var pattern in new[] { "\nDAN={\n", "\nDAN={\r\n", "\r\nDAN={\r\n" })
        {
            danPos = rawText.IndexOf(pattern, StringComparison.Ordinal);
            if (danPos >= 0) { danPos += pattern.IndexOf("DAN"); break; }
        }

        if (danPos >= 0)
        {
            // Zeige ersten 3000 Zeichen des DAN-Blocks
            string danRaw = rawText.Substring(danPos, Math.Min(3000, rawText.Length - danPos));
            result["dan_raw_first3000"] = danRaw;

            // Finde das Ende des DAN-Blocks (erste } auf gleicher Ebene)
            int blockStart = rawText.IndexOf('{', danPos);
            int depth = 0, blockEnd = blockStart;
            for (int i = blockStart; i < Math.Min(rawText.Length, blockStart + 500000); i++)
            {
                if (rawText[i] == '{') depth++;
                else if (rawText[i] == '}') { depth--; if (depth == 0) { blockEnd = i; break; } }
            }
            result["dan_block_length_chars"] = blockEnd - blockStart;
            result["dan_block_occurrence_count"] = Regex.Matches(rawText, @"\nDAN=\{").Count;

            // Zähle key=value Paare im Rohtext-Block
            string danBlock = rawText.Substring(blockStart + 1, blockEnd - blockStart - 1);
            var topLevelKeys = new List<string>();
            int d2 = 0;
            bool inStr = false;
            for (int i = 0; i < danBlock.Length; i++)
            {
                char c = danBlock[i];
                if (c == '"') inStr = !inStr;
                if (inStr) continue;
                if (c == '{') d2++;
                else if (c == '}') d2--;
                else if (c == '=' && d2 == 0)
                {
                    // Finde den Key vor dem =
                    int keyEnd = i;
                    while (keyEnd > 0 && char.IsWhiteSpace(danBlock[keyEnd - 1])) keyEnd--;
                    int keyStart = keyEnd;
                    while (keyStart > 0 && !char.IsWhiteSpace(danBlock[keyStart - 1])
                        && danBlock[keyStart - 1] != '}' && danBlock[keyStart - 1] != '{')
                        keyStart--;
                    string key = danBlock.Substring(keyStart, keyEnd - keyStart);
                    if (!string.IsNullOrEmpty(key) && topLevelKeys.Count < 200)
                        topLevelKeys.Add(key);
                }
            }
            result["dan_raw_top_level_key_count"] = topLevelKeys.Count;
            result["dan_raw_top_level_keys"] = string.Join(", ", topLevelKeys);
        }
        else
        {
            result["dan_not_found"] = true;
            // Zeige alle Zeilen die "DAN" enthalten
            var danLines = rawText.Split('\n')
                .Select((l, i) => new { line = i, text = l.Trim() })
                .Where(x => x.text.StartsWith("DAN"))
                .Take(5)
                .Select(x => $"Line {x.line}: {x.text.Substring(0, Math.Min(80, x.text.Length))}")
                .ToList();
            result["dan_lines_sample"] = danLines;
        }

        // 2. Vergleich: Parsed DAN vs Raw DAN
        var root = new ClausewitzParser(rawText).Parse();
        if (root.TryGetValue("DAN", out var danParsed) && danParsed is Dictionary<string,object> danDict)
        {
            result["dan_parsed_key_count"] = danDict.Count;
            result["dan_parsed_keys"] = string.Join(", ", danDict.Keys.Take(60));
            result["dan_has_army_parsed"] = danDict.ContainsKey("army");
            result["dan_has_ledger_parsed"] = danDict.ContainsKey("ledger");
            result["dan_has_manpower_parsed"] = danDict.ContainsKey("manpower");
        }

        return Json(result);
    }

    [HttpGet]
    public IActionResult Index() => Content(@"<html><body style='font-family:monospace;background:#111;color:#eee;padding:2rem'>
<h2>EU4 Diagnose v8 - Raw Text</h2>
<form method='post' action='/Diagnostic/Inspect' enctype='multipart/form-data'>
<input type='file' name='saveFile' accept='.eu4,.zip' style='color:#eee'><br><br>
<button type='submit' style='padding:8px 20px;background:#ffc107;border:none;cursor:pointer'>Diagnose</button>
</form></body></html>", "text/html");
}
