using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Sap2000WinFormsSample
{
    public class DocumentationEntry
    {
        public string category { get; set; }
        public string method { get; set; }
        public string summary { get; set; }
        public List<string> parameters { get; set; }
        public string returns { get; set; }
        public string usage { get; set; }
        public List<string> keywords { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(method);
            if (!string.IsNullOrWhiteSpace(summary))
                sb.Append(": ").Append(summary);
            if (parameters != null && parameters.Count > 0)
                sb.Append(" Parameters: ").Append(string.Join(", ", parameters));
            if (!string.IsNullOrWhiteSpace(returns))
                sb.Append(" Returns: ").Append(returns);
            if (!string.IsNullOrWhiteSpace(usage))
                sb.Append(" Usage: ").Append(usage);
            return sb.ToString();
        }
    }

    public class Sap2000DocumentationLibrary
    {
        private readonly List<DocumentationEntry> _entries;

        public Sap2000DocumentationLibrary(string baseDirectory = null)
        {
            _entries = LoadEntries(baseDirectory) ?? new List<DocumentationEntry>();
        }

        public bool HasEntries => _entries.Count > 0;

        public string BuildContextSummary(string userPrompt, int maxEntries = 5)
        {
            if (_entries.Count == 0 || string.IsNullOrWhiteSpace(userPrompt))
                return string.Empty;

            userPrompt = userPrompt.ToLowerInvariant();

            var scored = new List<(DocumentationEntry entry, double score)>();

            foreach (var entry in _entries)
            {
                double score = 0;
                if (!string.IsNullOrWhiteSpace(entry.method) && userPrompt.Contains(entry.method.ToLowerInvariant()))
                    score += 5;

                if (entry.keywords != null)
                {
                    foreach (var keyword in entry.keywords)
                    {
                        if (string.IsNullOrWhiteSpace(keyword)) continue;
                        if (userPrompt.Contains(keyword.ToLowerInvariant()))
                            score += 1.5;
                    }
                }

                if (!string.IsNullOrWhiteSpace(entry.category) && userPrompt.Contains(entry.category.ToLowerInvariant()))
                    score += 1;

                if (score > 0)
                {
                    scored.Add((entry, score));
                }
            }

            if (scored.Count == 0)
            {
                // Provide a default set emphasizing fundamentals to keep planner grounded in documentation.
                scored = _entries
                    .OrderBy(e => CategoryPriority(e.category))
                    .ThenBy(e => e.method, StringComparer.OrdinalIgnoreCase)
                    .Take(maxEntries)
                    .Select(e => (e, 0.1))
                    .ToList();
            }
            else
            {
                scored = scored
                    .OrderByDescending(tuple => tuple.score)
                    .ThenBy(tuple => tuple.entry.method, StringComparer.OrdinalIgnoreCase)
                    .Take(maxEntries)
                    .ToList();
            }

            var builder = new StringBuilder();
            builder.AppendLine("Relevant CSI OAPI (SAP2000 v26) references:");

            foreach (var (entry, _) in scored)
            {
                builder.Append("- ");
                builder.AppendLine(entry.ToString());
            }

            builder.AppendLine("Always follow documented call order and respect required units and load cases.");

            return builder.ToString();
        }

        private static int CategoryPriority(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return 99;
            category = category.ToLowerInvariant();
            if (category.Contains("model initialization")) return 0;
            if (category.Contains("units")) return 1;
            if (category.Contains("geometry")) return 2;
            if (category.Contains("loads")) return 3;
            if (category.Contains("analysis")) return 4;
            if (category.Contains("information")) return 5;
            return 50;
        }

        private static List<DocumentationEntry> LoadEntries(string baseDirectory)
        {
            try
            {
                string resolvedBase = baseDirectory;
                if (string.IsNullOrWhiteSpace(resolvedBase))
                    resolvedBase = AppDomain.CurrentDomain.BaseDirectory;

                string[] potentialPaths = new[]
                {
                    Path.Combine(resolvedBase, "Documentation", "CSI_OAPI_v26_Methods.json"),
                    Path.Combine(AppContext.BaseDirectory, "Documentation", "CSI_OAPI_v26_Methods.json"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CSI_OAPI_v26_Methods.json"),
                    Path.Combine(Environment.CurrentDirectory, "Documentation", "CSI_OAPI_v26_Methods.json")
                };

                string path = potentialPaths.FirstOrDefault(File.Exists);

                if (path == null && !string.IsNullOrWhiteSpace(baseDirectory))
                {
                    path = Path.Combine(baseDirectory, "Documentation", "CSI_OAPI_v26_Methods.json");
                    if (!File.Exists(path))
                        path = null;
                }

                if (path == null)
                {
                    return null;
                }

                var json = File.ReadAllText(path);
                var entries = JsonSerializer.Deserialize<List<DocumentationEntry>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return entries?.Where(e => !string.IsNullOrWhiteSpace(e?.method)).ToList();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
