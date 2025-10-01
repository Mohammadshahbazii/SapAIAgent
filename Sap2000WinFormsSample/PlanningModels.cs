using System.Collections.Generic;

namespace Sap2000WinFormsSample
{
    public class Plan
    {
        public string intent { get; set; }                 // e.g., "design_reservoir"
        public List<PlanStep> steps { get; set; }
    }

    public class PlanStep
    {
        public string action { get; set; }                 // e.g., "InitializeBlankModel"
        public Dictionary<string, object> @params { get; set; }
        public bool confirm { get; set; }                  // ask user if true (for destructive ops)
    }

    public class PlannerTurn
    {
        public string status { get; set; }
        public string message { get; set; }
        public List<string> questions { get; set; }
        public Plan plan { get; set; }
        public string rawContent { get; set; }
    }

    public class ChatMessage
    {
        public string Role { get; }
        public string Content { get; }

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }

        public static ChatMessage User(string content) => new ChatMessage("user", content);
        public static ChatMessage Assistant(string content) => new ChatMessage("assistant", content);
    }
}
