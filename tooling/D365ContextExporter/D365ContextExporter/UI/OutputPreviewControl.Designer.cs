namespace D365ContextExporter.UI
{
    partial class OutputPreviewControl
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
            this.mainLayout = new System.Windows.Forms.TableLayoutPanel();
            this.lblBanner = new System.Windows.Forms.Label();
            this.lblProjectOutput = new System.Windows.Forms.Label();
            this.txtProjectOutput = new System.Windows.Forms.TextBox();
            this.projectButtonPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.btnOpenProject = new System.Windows.Forms.Button();
            this.btnRevealProject = new System.Windows.Forms.Button();
            this.btnCopyProject = new System.Windows.Forms.Button();
            this.mainLayout.SuspendLayout();
            this.projectButtonPanel.SuspendLayout();
            this.SuspendLayout();

            // mainLayout
            this.mainLayout.ColumnCount = 2;
            this.mainLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.mainLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainLayout.Controls.Add(this.lblBanner, 0, 0);
            this.mainLayout.Controls.Add(this.lblProjectOutput, 0, 1);
            this.mainLayout.Controls.Add(this.txtProjectOutput, 1, 1);
            this.mainLayout.Controls.Add(this.projectButtonPanel, 0, 2);
            this.mainLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainLayout.Name = "mainLayout";
            this.mainLayout.RowCount = 3;
            this.mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.mainLayout.Size = new System.Drawing.Size(784, 80);
            this.mainLayout.TabIndex = 0;

            // lblBanner
            this.mainLayout.SetColumnSpan(this.lblBanner, 2);
            this.lblBanner.AutoSize = true;
            this.lblBanner.BackColor = System.Drawing.Color.FromArgb(232, 244, 255);
            this.lblBanner.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblBanner.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblBanner.ForeColor = System.Drawing.Color.FromArgb(0, 84, 166);
            this.lblBanner.Name = "lblBanner";
            this.lblBanner.Padding = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.lblBanner.TabIndex = 0;
            this.lblBanner.Text = "Export complete. Upload the context file to your AI assistant.";

            // lblProjectOutput
            this.lblProjectOutput.AutoSize = true;
            this.lblProjectOutput.Name = "lblProjectOutput";
            this.lblProjectOutput.Padding = new System.Windows.Forms.Padding(0, 5, 6, 0);
            this.lblProjectOutput.TabIndex = 1;
            this.lblProjectOutput.Text = "Context file:";

            // txtProjectOutput
            this.txtProjectOutput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtProjectOutput.Name = "txtProjectOutput";
            this.txtProjectOutput.ReadOnly = true;
            this.txtProjectOutput.TabIndex = 2;

            // projectButtonPanel
            this.mainLayout.SetColumnSpan(this.projectButtonPanel, 2);
            this.projectButtonPanel.AutoSize = true;
            this.projectButtonPanel.Controls.Add(this.btnOpenProject);
            this.projectButtonPanel.Controls.Add(this.btnRevealProject);
            this.projectButtonPanel.Controls.Add(this.btnCopyProject);
            this.projectButtonPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.projectButtonPanel.Name = "projectButtonPanel";
            this.projectButtonPanel.Padding = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this.projectButtonPanel.TabIndex = 3;

            // btnOpenProject
            this.btnOpenProject.Name = "btnOpenProject";
            this.btnOpenProject.Size = new System.Drawing.Size(60, 23);
            this.btnOpenProject.TabIndex = 0;
            this.btnOpenProject.Text = "Open";
            this.btnOpenProject.UseVisualStyleBackColor = true;
            this.btnOpenProject.Click += new System.EventHandler(this.btnOpenProject_Click);

            // btnRevealProject
            this.btnRevealProject.Name = "btnRevealProject";
            this.btnRevealProject.Size = new System.Drawing.Size(60, 23);
            this.btnRevealProject.TabIndex = 1;
            this.btnRevealProject.Text = "Reveal";
            this.btnRevealProject.UseVisualStyleBackColor = true;
            this.btnRevealProject.Click += new System.EventHandler(this.btnRevealProject_Click);

            // btnCopyProject
            this.btnCopyProject.Name = "btnCopyProject";
            this.btnCopyProject.Size = new System.Drawing.Size(75, 23);
            this.btnCopyProject.TabIndex = 2;
            this.btnCopyProject.Text = "Copy Path";
            this.btnCopyProject.UseVisualStyleBackColor = true;
            this.btnCopyProject.Click += new System.EventHandler(this.btnCopyProject_Click);

            // OutputPreviewControl
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.mainLayout);
            this.Name = "OutputPreviewControl";
            this.Size = new System.Drawing.Size(784, 80);
            this.mainLayout.ResumeLayout(false);
            this.mainLayout.PerformLayout();
            this.projectButtonPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.TableLayoutPanel mainLayout;
        private System.Windows.Forms.Label lblBanner;
        private System.Windows.Forms.Label lblProjectOutput;
        private System.Windows.Forms.TextBox txtProjectOutput;
        private System.Windows.Forms.FlowLayoutPanel projectButtonPanel;
        private System.Windows.Forms.Button btnOpenProject;
        private System.Windows.Forms.Button btnRevealProject;
        private System.Windows.Forms.Button btnCopyProject;
    }
}
