using System;
using System.Collections.Generic;
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

        public async Task<PlannerTurn> MakePlanAsync(List<ChatMessage> conversation, string toolsCatalog)
        {
            if (conversation == null || conversation.Count == 0)
                throw new ApplicationException("Conversation must contain at least one user message.");

            var system = @"You are a conversational planner for a SAP2000 automation agent.
When the user prompt is incomplete, ask clarifying questions to gather the missing parameters before producing a plan.
Always reply with STRICT JSON (no markdown, no commentary) that matches this schema:
{
  ""status"": ""plan_ready|need_clarification|error"",
  ""message"": ""string"",
  ""questions"": [""question1"", ""question2""]?,
  ""plan"": {
    ""intent"": ""string"",
    ""steps"": [
      { ""action"": ""ActionName"", ""params"": { /* key: value */ }, ""confirm"": false }
    ]
  }?
}
Rules:
- If you need more information, set status=""need_clarification"", include a helpful message, and add one or more concrete questions. Omit the plan in that case.
- When ready, set status=""plan_ready"", include a concise summary message, and provide a valid plan using only the allowed tools.
- For errors, set status=""error"" and explain the issue in the message.
- Use only actions from the provided tools catalog.
- Be explicit with numeric values and units choice.
- Prefer SI units unless the user specifies otherwise.
- Keep steps minimal and executable.";

            var payload = new
            {
                model = "gpt-4o", // e.g., "gpt-4o-mini"
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "assistant", content = "Available tools:\n" + toolsCatalog },
                }.Concat(conversation.Select(m => new { role = m.Role, content = m.Content })),
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

            var turn = JsonSerializer.Deserialize<PlannerTurn>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (turn == null)
                throw new ApplicationException("Planner returned invalid JSON.");

            turn.rawContent = content;
            return turn;
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
