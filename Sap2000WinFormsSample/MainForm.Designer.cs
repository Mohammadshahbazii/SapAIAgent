namespace Sap2000WinFormsSample
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Button btnStartNew;
        private System.Windows.Forms.Button btnAttachRunning;
        private System.Windows.Forms.Button btnInitBlank;
        private System.Windows.Forms.Button btnSetUnits;
        private System.Windows.Forms.Button btnSaveModel;
        private System.Windows.Forms.Button btnReadInfo;
        private System.Windows.Forms.Button btnCloseSap;
        private System.Windows.Forms.TextBox txtLog;
        private System.Windows.Forms.TextBox txtSavePath;
        private System.Windows.Forms.Label lblPath;
        private System.Windows.Forms.ComboBox cmbUnits;
        private System.Windows.Forms.TextBox txtApiKey;
        private System.Windows.Forms.TextBox txtAiPrompt;
        private System.Windows.Forms.Button btnAiDesign;


        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.btnStartNew = new System.Windows.Forms.Button();
            this.btnAttachRunning = new System.Windows.Forms.Button();
            this.btnInitBlank = new System.Windows.Forms.Button();
            this.btnSetUnits = new System.Windows.Forms.Button();
            this.btnSaveModel = new System.Windows.Forms.Button();
            this.btnReadInfo = new System.Windows.Forms.Button();
            this.btnCloseSap = new System.Windows.Forms.Button();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.txtSavePath = new System.Windows.Forms.TextBox();
            this.lblPath = new System.Windows.Forms.Label();
            this.cmbUnits = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // btnStartNew
            // 
            this.btnStartNew.Location = new System.Drawing.Point(16, 15);
            this.btnStartNew.Name = "btnStartNew";
            this.btnStartNew.Size = new System.Drawing.Size(170, 35);
            this.btnStartNew.TabIndex = 0;
            this.btnStartNew.Text = "Start New SAP2000";
            this.btnStartNew.UseVisualStyleBackColor = true;
            this.btnStartNew.Click += new System.EventHandler(this.btnStartNew_Click);
            // 
            // btnAttachRunning
            // 
            this.btnAttachRunning.Location = new System.Drawing.Point(196, 15);
            this.btnAttachRunning.Name = "btnAttachRunning";
            this.btnAttachRunning.Size = new System.Drawing.Size(170, 35);
            this.btnAttachRunning.TabIndex = 1;
            this.btnAttachRunning.Text = "Attach to Running";
            this.btnAttachRunning.UseVisualStyleBackColor = true;
            this.btnAttachRunning.Click += new System.EventHandler(this.btnAttachRunning_Click);
            // 
            // btnInitBlank
            // 
            this.btnInitBlank.Location = new System.Drawing.Point(16, 60);
            this.btnInitBlank.Name = "btnInitBlank";
            this.btnInitBlank.Size = new System.Drawing.Size(170, 35);
            this.btnInitBlank.TabIndex = 2;
            this.btnInitBlank.Text = "Initialize Blank Model";
            this.btnInitBlank.UseVisualStyleBackColor = true;
            this.btnInitBlank.Click += new System.EventHandler(this.btnInitBlank_Click);
            // 
            // btnSetUnits
            // 
            this.btnSetUnits.Location = new System.Drawing.Point(196, 60);
            this.btnSetUnits.Name = "btnSetUnits";
            this.btnSetUnits.Size = new System.Drawing.Size(90, 35);
            this.btnSetUnits.TabIndex = 3;
            this.btnSetUnits.Text = "Set Units";
            this.btnSetUnits.UseVisualStyleBackColor = true;
            this.btnSetUnits.Click += new System.EventHandler(this.btnSetUnits_Click);
            // 
            // cmbUnits
            // 
            this.cmbUnits.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbUnits.FormattingEnabled = true;
            this.cmbUnits.Location = new System.Drawing.Point(292, 66);
            this.cmbUnits.Name = "cmbUnits";
            this.cmbUnits.Size = new System.Drawing.Size(186, 24);
            this.cmbUnits.TabIndex = 10;
            // 
            // lblPath
            // 
            this.lblPath.AutoSize = true;
            this.lblPath.Location = new System.Drawing.Point(13, 110);
            this.lblPath.Name = "lblPath";
            this.lblPath.Size = new System.Drawing.Size(118, 16);
            this.lblPath.TabIndex = 7;
            this.lblPath.Text = "Save Model Path:";
            // 
            // txtSavePath
            // 
            this.txtSavePath.Location = new System.Drawing.Point(137, 107);
            this.txtSavePath.Name = "txtSavePath";
            this.txtSavePath.Size = new System.Drawing.Size(341, 22);
            this.txtSavePath.TabIndex = 6;
            this.txtSavePath.Text = "C:\\Temp\\Sap2000Sample.SDB";
            // 
            // btnSaveModel
            // 
            this.btnSaveModel.Location = new System.Drawing.Point(484, 104);
            this.btnSaveModel.Name = "btnSaveModel";
            this.btnSaveModel.Size = new System.Drawing.Size(120, 28);
            this.btnSaveModel.TabIndex = 5;
            this.btnSaveModel.Text = "Save Model";
            this.btnSaveModel.UseVisualStyleBackColor = true;
            this.btnSaveModel.Click += new System.EventHandler(this.btnSaveModel_Click);
            // 
            // btnReadInfo
            // 
            this.btnReadInfo.Location = new System.Drawing.Point(16, 145);
            this.btnReadInfo.Name = "btnReadInfo";
            this.btnReadInfo.Size = new System.Drawing.Size(170, 35);
            this.btnReadInfo.TabIndex = 8;
            this.btnReadInfo.Text = "Read Model Info";
            this.btnReadInfo.UseVisualStyleBackColor = true;
            this.btnReadInfo.Click += new System.EventHandler(this.btnReadInfo_Click);
            // 
            // btnCloseSap
            // 
            this.btnCloseSap.Location = new System.Drawing.Point(196, 145);
            this.btnCloseSap.Name = "btnCloseSap";
            this.btnCloseSap.Size = new System.Drawing.Size(170, 35);
            this.btnCloseSap.TabIndex = 9;
            this.btnCloseSap.Text = "Close SAP / Detach";
            this.btnCloseSap.UseVisualStyleBackColor = true;
            this.btnCloseSap.Click += new System.EventHandler(this.btnCloseSap_Click);
            // 
            // txtLog
            // 
            this.txtLog.Location = new System.Drawing.Point(16, 193);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLog.Size = new System.Drawing.Size(588, 210);
            this.txtLog.TabIndex = 4;
            // txtApiKey
            this.txtApiKey = new System.Windows.Forms.TextBox();
            this.txtApiKey.Location = new System.Drawing.Point(16, 410);
            this.txtApiKey.Name = "txtApiKey";
            this.txtApiKey.Size = new System.Drawing.Size(588, 22);
            this.txtApiKey.TabIndex = 11;
            //this.txtApiKey.PlaceholderText = "OpenAI API Key (or set env var OPENAI_API_KEY)";

            // txtAiPrompt
            this.txtAiPrompt = new System.Windows.Forms.TextBox();
            this.txtAiPrompt.Location = new System.Drawing.Point(16, 438);
            this.txtAiPrompt.Multiline = true;
            this.txtAiPrompt.Name = "txtAiPrompt";
            this.txtAiPrompt.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtAiPrompt.Size = new System.Drawing.Size(588, 100);
            this.txtAiPrompt.TabIndex = 12;
            this.txtAiPrompt.Text = "یک مخزن با ظرفیت 1000 مترمکعب، ارتفاع حدود 6 متر، طراحی اقتصادی برای فولاد ST37.";

            // btnAiDesign
            this.btnAiDesign = new System.Windows.Forms.Button();
            this.btnAiDesign.Location = new System.Drawing.Point(16, 544);
            this.btnAiDesign.Name = "btnAiDesign";
            this.btnAiDesign.Size = new System.Drawing.Size(170, 35);
            this.btnAiDesign.TabIndex = 13;
            this.btnAiDesign.Text = "Design with AI";
            this.btnAiDesign.UseVisualStyleBackColor = true;
            this.btnAiDesign.Click += new System.EventHandler(this.btnAiDesign_Click);

            // Add to Controls
            this.Controls.Add(this.txtApiKey);
            this.Controls.Add(this.txtAiPrompt);
            this.Controls.Add(this.btnAiDesign);

            // Increase form height a bit
            this.ClientSize = new System.Drawing.Size(622, 590);

            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.ClientSize = new System.Drawing.Size(622, 421);
            this.Controls.Add(this.cmbUnits);
            this.Controls.Add(this.btnCloseSap);
            this.Controls.Add(this.btnReadInfo);
            this.Controls.Add(this.btnSaveModel);
            this.Controls.Add(this.lblPath);
            this.Controls.Add(this.txtSavePath);
            this.Controls.Add(this.txtLog);
            this.Controls.Add(this.btnSetUnits);
            this.Controls.Add(this.btnInitBlank);
            this.Controls.Add(this.btnAttachRunning);
            this.Controls.Add(this.btnStartNew);
            this.Name = "MainForm";
            this.Text = "SAP2000 API — WinForms Sample (v26)";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
