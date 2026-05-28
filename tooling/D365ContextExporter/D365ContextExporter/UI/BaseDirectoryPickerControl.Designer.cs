namespace Lspiguel.Xrm.D365ContextExporter.UI
{
    partial class BaseDirectoryPickerControl
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
            this.lblBaseDir = new System.Windows.Forms.Label();
            this.tableLayout = new System.Windows.Forms.TableLayoutPanel();
            this.txtBaseDir = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.tableLayout.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblBaseDir
            // 
            this.lblBaseDir.AutoSize = true;
            this.lblBaseDir.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblBaseDir.Location = new System.Drawing.Point(0, 0);
            this.lblBaseDir.Name = "lblBaseDir";
            this.lblBaseDir.Size = new System.Drawing.Size(157, 13);
            this.lblBaseDir.TabIndex = 0;
            this.lblBaseDir.Text = "Context-Exporter base directory:";
            // 
            // tableLayout
            // 
            this.tableLayout.ColumnCount = 2;
            this.tableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 69F));
            this.tableLayout.Controls.Add(this.txtBaseDir, 0, 0);
            this.tableLayout.Controls.Add(this.btnBrowse, 1, 0);
            this.tableLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayout.Location = new System.Drawing.Point(0, 13);
            this.tableLayout.Name = "tableLayout";
            this.tableLayout.RowCount = 1;
            this.tableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayout.Size = new System.Drawing.Size(343, 30);
            this.tableLayout.TabIndex = 1;
            // 
            // txtBaseDir
            // 
            this.txtBaseDir.BackColor = System.Drawing.SystemColors.Window;
            this.txtBaseDir.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtBaseDir.Location = new System.Drawing.Point(3, 3);
            this.txtBaseDir.Name = "txtBaseDir";
            this.txtBaseDir.ReadOnly = true;
            this.txtBaseDir.Size = new System.Drawing.Size(268, 20);
            this.txtBaseDir.TabIndex = 0;
            // 
            // btnBrowse
            // 
            this.btnBrowse.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnBrowse.Location = new System.Drawing.Point(277, 3);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(63, 24);
            this.btnBrowse.TabIndex = 1;
            this.btnBrowse.Text = "Browse";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // BaseDirectoryPickerControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayout);
            this.Controls.Add(this.lblBaseDir);
            this.Name = "BaseDirectoryPickerControl";
            this.Size = new System.Drawing.Size(343, 43);
            this.tableLayout.ResumeLayout(false);
            this.tableLayout.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.Label lblBaseDir;
        private System.Windows.Forms.TableLayoutPanel tableLayout;
        private System.Windows.Forms.TextBox txtBaseDir;
        private System.Windows.Forms.Button btnBrowse;
    }
}
