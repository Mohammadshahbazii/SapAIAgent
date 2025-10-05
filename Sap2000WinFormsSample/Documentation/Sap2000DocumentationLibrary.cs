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
        private readonly Dictionary<string, DocumentationEntry> _methodIndex;

        public Sap2000DocumentationLibrary(string baseDirectory = null)
        {
            _entries = LoadEntries(baseDirectory) ?? new List<DocumentationEntry>();
            _methodIndex = BuildMethodIndex(_entries);
        }

        public bool HasEntries => _entries.Count > 0;

        public bool TryGetEntry(string methodName, out DocumentationEntry entry)
        {
            if (string.IsNullOrWhiteSpace(methodName) || _methodIndex == null)
            {
                entry = null;
                return false;
            }

            return _methodIndex.TryGetValue(methodName, out entry);
        }

        public string BuildMethodSummary(IEnumerable<string> methodNames, int maxEntries = 5)
        {
            if (_methodIndex == null || methodNames == null)
                return string.Empty;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sb = new StringBuilder();
            int count = 0;

            foreach (var method in methodNames)
            {
                if (count >= maxEntries)
                    break;

                if (string.IsNullOrWhiteSpace(method) || !seen.Add(method))
                    continue;

                if (!_methodIndex.TryGetValue(method, out var entry))
                    continue;

                sb.Append("- ");
                sb.Append(entry.method);

                if (!string.IsNullOrWhiteSpace(entry.summary))
                    sb.Append(": ").Append(entry.summary);

                if (entry.parameters != null && entry.parameters.Count > 0)
                    sb.Append(" Parameters: ").Append(string.Join("; ", entry.parameters));

                if (!string.IsNullOrWhiteSpace(entry.usage))
                    sb.Append(" Usage: ").Append(entry.usage);

                if (!string.IsNullOrWhiteSpace(entry.returns))
                    sb.Append(" Returns: ").Append(entry.returns);

                sb.AppendLine();
                count++;
            }

            return count > 0 ? sb.ToString().TrimEnd() : string.Empty;
        }

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

        private static Dictionary<string, DocumentationEntry> BuildMethodIndex(IEnumerable<DocumentationEntry> entries)
        {
            var index = new Dictionary<string, DocumentationEntry>(StringComparer.OrdinalIgnoreCase);

            if (entries == null)
                return index;

            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry?.method))
                    continue;

                if (!index.ContainsKey(entry.method))
                    index[entry.method] = entry;
            }

            return index;
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
