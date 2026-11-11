using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    // The words on screen in each language (Docs/Strings.md, 2026-11-11, the research report's candidate 17). The game
    // is written in Korean, and the code keeps each line's Korean beside its id, so a game without the sheet, or a line
    // the sheet lacks in a language, shows the Korean. The sheet adds the other languages, a column each.
    public sealed class StringData
    {
        public static readonly StringData Empty = new StringData(new Dictionary<string, Dictionary<string, string>>(), Array.Empty<string>(), new[] { "ko" });

        readonly Dictionary<string, Dictionary<string, string>> _lines;

        public StringData(Dictionary<string, Dictionary<string, string>> lines, IReadOnlyList<string> errors, IReadOnlyList<string> languages)
        {
            _lines = lines;
            Errors = errors;
            Languages = languages;
        }

        public IReadOnlyList<string> Errors { get; }
        public bool Success => Errors.Count == 0;
        // The languages the sheet has columns for, Korean first.
        public IReadOnlyList<string> Languages { get; }
        public int Count => _lines.Count;

        // The line in that language, else the Korean the caller gave.
        public string Get(string id, string language, string korean) =>
            _lines.TryGetValue(id, out var by) && by.TryGetValue(language, out var text) && text.Length > 0 ? text : korean;

        // How many lines have words in the language.
        public int Filled(string language) => _lines.Values.Count(by => by.TryGetValue(language, out var text) && text.Length > 0);
    }

    // Reads Assets/_Project/Data/Strings/strings.csv: id, ko, then a column per language (en first).
    public static class StringSheet
    {
        public const string File = "strings.csv";

        public static StringData Parse(string csv)
        {
            var errors = new List<string>();
            var lines = new Dictionary<string, Dictionary<string, string>>();
            if (string.IsNullOrWhiteSpace(csv)) return StringData.Empty;
            var table = CsvTable.Read(csv);
            var missing = table.MissingColumns("id", "ko");
            if (missing.Count > 0)
            {
                errors.Add($"{File}: 열이 없습니다: {string.Join(", ", missing)}");
                return new StringData(lines, errors, new[] { "ko" });
            }
            var languages = new[] { "ko" }.Concat(table.Header.Where(c => c != "id" && c != "ko" && c.Length > 0)).ToList();
            foreach (var row in table.Rows)
            {
                string id = row["id"];
                if (id.Length == 0 || row["ko"].Length == 0)
                {
                    errors.Add($"{File} {row.Line}행: id와 ko를 적어야 합니다.");
                    continue;
                }
                if (lines.ContainsKey(id))
                {
                    errors.Add($"{File} {row.Line}행: '{id}'가 두 번 있습니다.");
                    continue;
                }
                lines[id] = languages.ToDictionary(language => language, language => row[language]);
            }
            return new StringData(lines, errors, languages);
        }
    }
}
