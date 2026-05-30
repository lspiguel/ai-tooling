namespace Lspiguel.Xrm.D365ContextExporter.UI
{
    partial class ExportProgressControl
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.headerPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.lblProgress = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.spinnerProgress = new System.Windows.Forms.ProgressBar();
            this.rtbLog = new System.Windows.Forms.RichTextBox();
            this.headerPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // headerPanel
            // 
            this.headerPanel.AutoSize = false;
            this.headerPanel.Controls.Add(this.lblProgress);
            this.headerPanel.Controls.Add(this.btnCancel);
            this.headerPanel.Controls.Add(this.spinnerProgress);
            this.headerPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.headerPanel.Location = new System.Drawing.Point(0, 0);
            this.headerPanel.Name = "headerPanel";
            this.headerPanel.Padding = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.headerPanel.Size = new System.Drawing.Size(343, 30);
            this.headerPanel.TabIndex = 1;
            // 
            // lblProgress
            // 
            this.lblProgress.AutoSize = true;
            this.lblProgress.Location = new System.Drawing.Point(3, 9);
            this.lblProgress.Margin = new System.Windows.Forms.Padding(3, 7, 3, 0);
            this.lblProgress.Name = "lblProgress";
            this.lblProgress.Size = new System.Drawing.Size(0, 13);
            this.lblProgress.TabIndex = 0;
            this.lblProgress.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnCancel
            // 
            this.btnCancel.Enabled = false;
            this.btnCancel.Location = new System.Drawing.Point(9, 5);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(64, 20);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            //
            // spinnerProgress
            //
            this.spinnerProgress.MarqueeAnimationSpeed = 40;
            this.spinnerProgress.Name = "spinnerProgress";
            this.spinnerProgress.Size = new System.Drawing.Size(80, 20);
            this.spinnerProgress.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
            this.spinnerProgress.TabIndex = 2;
            this.spinnerProgress.Visible = false;
            //
            // rtbLog
            // 
            this.rtbLog.BackColor = System.Drawing.SystemColors.Window;
            this.rtbLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtbLog.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtbLog.Location = new System.Drawing.Point(0, 30);
            this.rtbLog.Name = "rtbLog";
            this.rtbLog.ReadOnly = true;
            this.rtbLog.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.rtbLog.Size = new System.Drawing.Size(343, 143);
            this.rtbLog.TabIndex = 0;
            this.rtbLog.Text = "";
            // 
            // ExportProgressControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.rtbLog);
            this.Controls.Add(this.headerPanel);
            this.Name = "ExportProgressControl";
            this.Size = new System.Drawing.Size(343, 173);
            this.headerPanel.ResumeLayout(false);
            this.headerPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.FlowLayoutPanel headerPanel;
        private System.Windows.Forms.Label lblProgress;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.ProgressBar spinnerProgress;
        private System.Windows.Forms.RichTextBox rtbLog;
    }
}
