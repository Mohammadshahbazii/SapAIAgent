using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using SAP2000v1;

namespace Sap2000WinFormsSample
{
    public partial class MainForm : Form
    {
        private cHelper _helper;
        private cOAPI _oapi;      // SAP2000 main object
        private cSapModel _model; // Active model
        private SkillRegistry _reg;
        private Sap2000DocumentationLibrary _documentation;
        private void EnsureSkills()
        {
            if (_reg != null) return;
            _reg = new SkillRegistry();
            _reg.Register(new InitializeBlankModelSkill());
            _reg.Register(new SetUnitsSkill());
            _reg.Register(new BuildCylindricalReservoirSkill());
            _reg.Register(new BuildMultiStoryBuildingSkill());
            _reg.Register(new ConfigureDesignCodesSkill());
            _reg.Register(new SetupAdvancedAnalysesSkill());
            _reg.Register(new SaveModelSkill());
            _reg.Register(new RunAnalysisSkill());
            _reg.Register(new GetModelInfoSkill());
        }

        public MainForm()
        {
            InitializeComponent();
            InitUi();

            try
            {
                _documentation = new Sap2000DocumentationLibrary();
                if (_documentation.HasEntries)
                    Log("Loaded SAP2000 CSI OAPI documentation index.");
                else
                    Log("Documentation index not found. AI planning will use skill catalog only.");
            }
            catch (Exception ex)
            {
                LogError("Failed to load documentation index: " + ex.Message);
            }
        }

        private void InitUi()
        {
            // Populate some common unit sets
            // (You can add/remove according to your workflow)
            cmbUnits.Items.Clear();
            cmbUnits.Items.Add(eUnits.kN_m_C);
            cmbUnits.Items.Add(eUnits.N_mm_C);
            cmbUnits.Items.Add(eUnits.kip_ft_F);
            cmbUnits.Items.Add(eUnits.kip_in_F);
            cmbUnits.SelectedIndex = 0;

            Log("Ready.");
        }

        private void btnStartNew_Click(object sender, EventArgs e)
        {
            try
            {
                Log("Starting new SAP2000 instance...");
                _helper = new Helper();
                _oapi = _helper.CreateObjectProgID("CSI.SAP2000.API.SapObject");
                _oapi.ApplicationStart(); // optionally pass true to start hidden: ApplicationStart(true)

                _model = _oapi.SapModel;
                Log("SAP2000 started and connected.");
            }
            catch (Exception ex)
            {
                LogError("Failed to start SAP2000: " + ex.Message);
            }
        }

        //private async void btnAiDesign_Click(object sender, EventArgs e)
        //{
        //    if (!EnsureConnected()) { MessageBox.Show("Connect to SAP2000 first."); return; }
        //    EnsureSkills();

        //    var apiKey = string.IsNullOrWhiteSpace(txtApiKey.Text) ? Environment.GetEnvironmentVariable("OPENAI_API_KEY") : txtApiKey.Text;
        //    if (string.IsNullOrWhiteSpace(apiKey)) { MessageBox.Show("Enter API key."); return; }

        //    var planner = new PlannerService(apiKey);
        //    var toolsCatalog = PlannerService.BuildToolsCatalog(_reg);

        //    btnAiDesign.Enabled = false;
        //    try
        //    {
        //        Log("Planning…");
        //        var plan = await planner.MakePlanAsync(txtAiPrompt.Text, toolsCatalog);
        //        Log("Plan received.");

        //        var orch = new Orchestrator(_model, _reg, Log);
        //        bool Confirm(string msg) => MessageBox.Show(msg, "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

        //        await orch.RunPlanAsync(plan, Confirm);
        //        Log("Done.");
        //    }
        //    catch (Exception ex)
        //    {
        //        LogError("Planner/Orchestrator error: " + ex.Message);
        //    }
        //    finally
        //    {
        //        btnAiDesign.Enabled = true;
        //    }

        //}

        private async void btnAiDesign_Click(object sender, EventArgs e)
        {
            if (!EnsureConnected())
            {
                MessageBox.Show("First connect to SAP2000 (Start New or Attach).");
                return;
            }

            EnsureSkills();

            try
            {
                string apiKey = txtApiKey.Text.Trim();
                if (string.IsNullOrWhiteSpace(apiKey))
                    apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    MessageBox.Show("Enter OpenAI API Key or set env var OPENAI_API_KEY.");
                    return;
                }

                var initialPrompt = txtAiPrompt.Text.Trim();
                if (string.IsNullOrWhiteSpace(initialPrompt))
                {
                    MessageBox.Show("Enter a description of what you want the agent to do.");
                    return;
                }

                btnAiDesign.Enabled = false;

                var planner = new PlannerService(apiKey);
                var toolsCatalog = PlannerService.BuildToolsCatalog(_reg);

                var conversation = new List<ChatMessage>
                {
                    ChatMessage.User(initialPrompt)
                };

                AppendDocumentationContext(conversation, initialPrompt);

                PlannerTurn turn = null;
                Plan finalPlan = null;

                while (true)
                {
                    Log("Asking planner to interpret conversation...");
                    turn = await planner.MakePlanAsync(conversation, toolsCatalog);
                    conversation.Add(ChatMessage.Assistant(turn.rawContent ?? "{}"));

                    if (string.Equals(turn.status, "need_clarification", StringComparison.OrdinalIgnoreCase))
                    {
                        var plannerMessage = turn.message ?? "The planner needs more information.";
                        Log(plannerMessage);
                        if (turn.questions == null || turn.questions.Count == 0)
                        {
                            LogError("Planner requested clarification without questions. Stopping.");
                            return;
                        }

                        foreach (var question in turn.questions)
                        {
                            Log($"Clarification requested: {question}");
                            var answer = PromptDialog.ShowClarification("Clarification needed", plannerMessage, question);
                            if (answer == null)
                            {
                                Log("User cancelled clarification. Aborting plan.");
                                return;
                            }

                            Log($"User provided: {answer}");
                            var clarification = $"Answer to question '{question}': {answer}";
                            conversation.Add(ChatMessage.User(clarification));
                            AppendDocumentationContext(conversation, clarification);
                        }

                        continue;
                    }

                    if (string.Equals(turn.status, "plan_ready", StringComparison.OrdinalIgnoreCase))
                    {
                        finalPlan = turn.plan;
                        Log(turn.message ?? "Plan ready.");
                        break;
                    }

                    LogError(turn.message ?? "Planner returned an error.");
                    return;
                }

                if (finalPlan == null)
                {
                    LogError("Planner did not return a plan.");
                    return;
                }

                Log($"Executing plan '{finalPlan.intent}' with {finalPlan.steps?.Count ?? 0} steps...");
                if (finalPlan.steps != null)
                {
                    foreach (var step in finalPlan.steps)
                    {
                        var confirmText = step.confirm ? "(confirm)" : string.Empty;
                        Log($" - {step.action} {confirmText}");
                    }
                }

                var orchestrator = new Orchestrator(_model, _reg, Log);
                bool Confirm(string msg) => MessageBox.Show(msg, "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

                await orchestrator.RunPlanAsync(finalPlan, Confirm);
                Log("Plan execution completed.");
            }
            catch (Exception ex)
            {
                LogError("AI planning failed: " + ex.Message);
            }
            finally
            {
                btnAiDesign.Enabled = true;
            }
        }



        private void btnAttachRunning_Click(object sender, EventArgs e)
        {
            try
            {
                Log("Attaching to running SAP2000...");
                _helper = new Helper();
                _oapi = _helper.GetObject("CSI.SAP2000.API.SapObject");
                _model = _oapi.SapModel;
                Log("Attached to running instance.");
            }
            catch (Exception ex)
            {
                LogError("Failed to attach: " + ex.Message);
            }
        }

        private void btnInitBlank_Click(object sender, EventArgs e)
        {
            if (!EnsureConnected()) return;

            try
            {
                int ret;

                Log("Initializing new blank model...");
                // Set default units first
                var units = (eUnits)cmbUnits.SelectedItem;

                ret = _model.InitializeNewModel(units);
                CheckRet(ret, "InitializeNewModel");

                ret = _model.File.NewBlank();
                CheckRet(ret, "File.NewBlank");

                Log("Blank model initialized with units: " + units);
            }
            catch (Exception ex)
            {
                LogError("Init blank failed: " + ex.Message);
            }
        }

        private void btnSetUnits_Click(object sender, EventArgs e)
        {
            if (!EnsureConnected()) return;

            try
            {
                var units = (eUnits)cmbUnits.SelectedItem;
                // IMPORTANT: Setting present units affects API inputs/outputs
                int ret = _model.SetPresentUnits(units);
                CheckRet(ret, "SetPresentUnits");
                Log("Units set to: " + units);
            }
            catch (Exception ex)
            {
                LogError("Set units failed: " + ex.Message);
            }
        }

        private void btnSaveModel_Click(object sender, EventArgs e)
        {
            if (!EnsureConnected()) return;

            try
            {
                var path = txtSavePath.Text.Trim();
                if (string.IsNullOrWhiteSpace(path))
                {
                    MessageBox.Show("Enter a valid path to save the model (.SDB).");
                    return;
                }
                var directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                Log("Saving model to: " + path);
                int ret = _model.File.Save(path);
                CheckRet(ret, "File.Save");
                Log("Model saved.");
            }
            catch (Exception ex)
            {
                LogError("Save failed: " + ex.Message);
            }
        }

        private void btnReadInfo_Click(object sender, EventArgs e)
        {
            if (!EnsureConnected()) return;

            try
            {
                string currentFile = string.Empty;

                // Corrected method call: GetModelFilename does not require 'out' keyword  
                string modelFilename = _model.GetModelFilename();
                currentFile = modelFilename;

                int numberNames = 0;
                string[] jointNames = null;
                int ret = _model.PointObj.GetNameList(ref numberNames, ref jointNames);
                CheckRet(ret, "PointObj.GetNameList");

                Log($"Current file: {currentFile}");
                Log($"Joint count: {numberNames}");
            }
            catch (Exception ex)
            {
                LogError("Read info failed: " + ex.Message);
            }
        }




        private void btnCloseSap_Click(object sender, EventArgs e)
        {
            try
            {
                if (_oapi != null)
                {
                    Log("Closing SAP2000...");
                    // Close current instance. To just detach, comment the following out:
                    _oapi.ApplicationExit(false); // false = prompt to save if needed
                    _oapi = null;
                    _model = null;
                    Log("Closed.");
                }
                else
                {
                    Log("No connection present.");
                }
            }
            catch (Exception ex)
            {
                LogError("Close failed: " + ex.Message);
            }
        }

        private bool EnsureConnected()
        {
            if (_oapi == null || _model == null)
            {
                MessageBox.Show("Not connected. Start or attach to SAP2000 first.", "SAP2000", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        private void CheckRet(int ret, string op)
        {
            if (ret != 0)
                throw new ApplicationException($"{op} returned nonzero code: {ret}");
        }

        private void Log(string msg)
        {
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
        }

        private void LogError(string msg)
        {
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ERROR: {msg}{Environment.NewLine}");
        }

        private void AppendDocumentationContext(List<ChatMessage> conversation, string userContent)
        {
            if (_documentation == null || conversation == null || string.IsNullOrWhiteSpace(userContent))
                return;

            var context = _documentation.BuildContextSummary(userContent);
            if (string.IsNullOrWhiteSpace(context))
                return;

            conversation.Add(ChatMessage.Assistant(context));
            Log("Added CSI OAPI documentation context for planner.");
        }
    }
}
