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
        private System.Windows.Forms.GroupBox grpCapabilities;
        private System.Windows.Forms.RichTextBox txtCapabilities;


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
            this.grpCapabilities = new System.Windows.Forms.GroupBox();
            this.txtCapabilities = new System.Windows.Forms.RichTextBox();
            this.grpCapabilities.SuspendLayout();
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

            // grpCapabilities
            this.grpCapabilities.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpCapabilities.Controls.Add(this.txtCapabilities);
            this.grpCapabilities.Location = new System.Drawing.Point(16, 585);
            this.grpCapabilities.Name = "grpCapabilities";
            this.grpCapabilities.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.grpCapabilities.Size = new System.Drawing.Size(588, 220);
            this.grpCapabilities.TabIndex = 14;
            this.grpCapabilities.TabStop = false;
            this.grpCapabilities.Text = "گستره خدمات طراحی";

            // txtCapabilities
            this.txtCapabilities.BackColor = System.Drawing.SystemColors.Control;
            this.txtCapabilities.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtCapabilities.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtCapabilities.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.txtCapabilities.Location = new System.Drawing.Point(3, 18);
            this.txtCapabilities.Name = "txtCapabilities";
            this.txtCapabilities.ReadOnly = true;
            this.txtCapabilities.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.txtCapabilities.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.None;
            this.txtCapabilities.Size = new System.Drawing.Size(582, 199);
            this.txtCapabilities.TabIndex = 0;
            this.txtCapabilities.TabStop = false;
            this.txtCapabilities.Text = "طراحی سازه‌های صنعتی و فلزی\n\n• سوله‌ها و سازه‌های صنعتی سنگین\n• سوله‌های فلزی با بادبند یا قاب خمشی\n• خرپاهای سنگین، جرثقیل‌دار، و سقف‌های شیبدار\n• پلتفرم‌های نفت و گاز\n• سازه‌های اسکله، دکل‌های ساحلی و فراساحلی\n• سازه‌های برجکی\n• برج خنک‌کننده، برج مخابراتی، برج نوری\n\n🛢 ۳. طراحی مخازن و سازه‌های تحت فشار\n\n• مخازن استوانه‌ای عمودی (Steel/Concrete)\n• تحلیل دیواره تحت فشار هیدرواستاتیک یا داخلی\n• طراحی ضخامت، سخت‌کننده‌ها، و فونداسیون\n• مخازن کروی یا افقی\n• تحت فشار داخلی یا بیرونی (Vacuum)\n• مخازن با سقف گنبدی یا مخروطی\n• تحلیل تنش در ناحیه انتقال سقف–دیواره\n• حوضچه‌ها و تانک‌های بتنی\n• مدل‌سازی با المان Shell یا Solid\n\n🌉 ۴. طراحی پل‌ها و سازه‌های زیرساختی\n\n• پل‌های تیرورقی، کابلی، قوسی و خرپایی\n• تحلیل سازه در اثر بارهای زنده، مرده، دما، زلزله، باد، و ترمز وسایل نقلیه\n• تحلیل زمان ساخت (Stage Construction)\n• افزودن تدریجی قطعات و تحلیل در هر مرحله\n• تحلیل دینامیکی پل‌ها تحت اثر زلزله یا عبور وسایل متحرک\n\n⚙️ ۵. سازه‌های خاص و غیرمتعارف\n\n• سازه‌های فضاکار (Space Frame / Dome)\n• تحلیل سه‌بعدی گنبدها و سقف‌های سبک\n• سازه‌های غشایی و کابلی (Tension Structures)\n• چادرها، سقف‌های پارچه‌ای، Membrane Roofs\n• سازه‌های ژئوتکنیکی ساده\n• دیواره نگهبان، فونداسیون شمعی، خاکریز مسلح (در حد مدل‌سازی خطی)\n• سازه‌های لرزه‌ای پیشرفته\n• میراگرها، جداسازهای لرزه‌ای (Base Isolator)";

            // Add to Controls
            this.Controls.Add(this.txtApiKey);
            this.Controls.Add(this.txtAiPrompt);
            this.Controls.Add(this.btnAiDesign);
            this.Controls.Add(this.grpCapabilities);

            // Increase form height a bit
            this.ClientSize = new System.Drawing.Size(622, 830);

            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.ClientSize = new System.Drawing.Size(622, 830);
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
            this.grpCapabilities.ResumeLayout(false);
            this.grpCapabilities.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
