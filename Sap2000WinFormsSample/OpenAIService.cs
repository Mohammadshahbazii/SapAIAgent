using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sap2000WinFormsSample
{
    public class OpenAIService
    {
        private readonly HttpClient _http;

        public OpenAIService(string apiKey)
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        public async Task<string> GetTankSpecJsonAsync(string userPrompt)
        {
            // Ask for STRICT JSON only. No prose.
            var system = @"You are a structural design assistant. 
Return STRICT JSON (no markdown, no commentary) matching this C#-friendly schema:
{
  ""type"": ""cylindrical_reservoir"",
  ""units"": { ""length"": ""m|mm|ft|in"", ""force"": ""kN|N|kip|lb"" },
  ""geometry"": { ""diameter"": number, ""height"": number, ""shellThickness"": number, ""numWallSegments"": integer, ""numHeightSegments"": integer },
  ""materials"": { ""steelGrade"": ""string"", ""yieldStress"": number },
  ""loads"": { ""liquidHeight"": number, ""unitWeight"": number, ""internalPressureKPa"": number },
  ""foundationElevation"": number
}
Rules:
- Only the JSON object, nothing else.
- Use numeric values (no units in numbers).
- Required: geometry.diameter > 0, geometry.height > 0
- Required: geometry.numWallSegments >= 12 and <= 64
- Required: geometry.numHeightSegments >= 2 and <= 40
- If user gives only volume and height, compute diameter from V = π*(D/2)^2*H.
- Prefer SI units (m, kN).";

            var assistantInstruction = @"Infer reasonable defaults if not specified. 
Prefer SI units (m, kN) unless the user clearly requests otherwise.";

            var payload = new
            {
                model = "gpt-4o", // e.g., "gpt-4o-mini"
                messages = new object[]
                {
                    new { role="system", content=system },
                    new { role="assistant", content=assistantInstruction },
                    new { role="user", content=userPrompt }
                },
                temperature = 0.2
            };

            var json = JsonSerializer.Serialize(payload);
            var req = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync("https://api.avalai.ir/v1/chat/completions", req);
            resp.EnsureSuccessStatusCode();

            var stream = await resp.Content.ReadAsStreamAsync();
            var doc = await JsonDocument.ParseAsync(stream);

            // Defensive parse: choices[0].message.content
            var root = doc.RootElement;
            var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            if (string.IsNullOrWhiteSpace(content))
                throw new ApplicationException("Empty AI response.");

            // Ensure it's JSON (strip accidental code fences if any)
            content = content.Trim();
            if (content.StartsWith("```"))
            {
                var idx = content.IndexOf('{');
                var last = content.LastIndexOf('}');
                if (idx >= 0 && last > idx) content = content.Substring(idx, last - idx + 1);
            }

            return content;
        }
    }
}
