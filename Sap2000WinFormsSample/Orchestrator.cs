using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using SAP2000v1;

namespace Sap2000WinFormsSample
{
    public class Orchestrator
    {
        private readonly cSapModel _model;
        private readonly SkillRegistry _skills;
        private readonly Action<string> _log;

        public Orchestrator(cSapModel model, SkillRegistry skills, Action<string> logger)
        {
            _model = model;
            _skills = skills;
            _log = logger;
        }

        public async Task RunPlanAsync(Plan plan, Func<string, bool> confirmIfNeeded = null)
        {
            if (plan == null || plan.steps == null || plan.steps.Count == 0)
            {
                _log("No steps to run.");
                return;
            }

            _log($"Intent: {plan.intent}");
            foreach (var step in plan.steps)
            {
                try
                {
                    if (step.confirm && confirmIfNeeded != null)
                    {
                        bool ok = confirmIfNeeded($"Confirm action '{step.action}'?");
                        if (!ok) { _log($"Skipped '{step.action}' by user choice."); continue; }
                    }

                    if (!_skills.TryGet(step.action, out var skill))
                        throw new ApplicationException($"Unknown action: {step.action}");

                    var result = skill.Execute(_model, step.@params);
                    _log($"✓ {step.action}: {result}");
                    await Task.Yield(); // keep UI responsive
                }
                catch (Exception ex)
                {
                    _log($"✗ {step.action}: ERROR {ex.Message}");
                    // Optionally: ask LLM for recovery; for now, stop.
                    break;
                }
            }
        }
    }
}
