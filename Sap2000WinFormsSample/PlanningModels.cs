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
}
