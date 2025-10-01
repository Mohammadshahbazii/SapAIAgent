using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sap2000WinFormsSample
{
    public class PlannerService
    {
        private readonly HttpClient _http;

        public PlannerService(string apiKey)
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        public async Task<Plan> MakePlanAsync(string userPrompt, string toolsCatalog)
        {
            var system = @"You are a task planner for a SAP2000 automation agent.
Return STRICT JSON only with this schema (no comments, no markdown):
{
  ""intent"": ""string"",
  ""steps"": [
    { ""action"": ""ActionName"", ""params"": { /* key: value */ }, ""confirm"": false }
  ]
}
Rules:
- Use only actions from the provided tools catalog.
- Be explicit with numeric values and units choice.
- Prefer SI units unless user specifies otherwise.
- Keep steps minimal and executable.";

            var payload = new
            {
                model = "gpt-4o", // e.g., "gpt-4o-mini"
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "assistant", content = "Available tools:\n" + toolsCatalog },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.2
            };

            var reqJson = JsonSerializer.Serialize(payload);
            var resp = await _http.PostAsync("https://api.avalai.ir/v1/chat/completions",
                new StringContent(reqJson, Encoding.UTF8, "application/json"));
            resp.EnsureSuccessStatusCode();

            var raw = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(raw);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            if (string.IsNullOrWhiteSpace(content))
                throw new ApplicationException("Planner returned empty content.");

            // strip accidental fences
            content = content.Trim();
            if (content.StartsWith("```"))
            {
                var i = content.IndexOf('{'); var j = content.LastIndexOf('}');
                if (i >= 0 && j > i) content = content.Substring(i, j - i + 1);
            }

            return JsonSerializer.Deserialize<Plan>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        public static string BuildToolsCatalog(SkillRegistry reg)
        {
            // Describe tools for the LLM
            var sb = new StringBuilder();
            foreach (var s in reg.All())
            {
                sb.AppendLine($"- {s.Name}: {s.Description}");
                sb.AppendLine($"  params: {s.ParamsSchema}");
            }
            return sb.ToString();
        }
    }
}
