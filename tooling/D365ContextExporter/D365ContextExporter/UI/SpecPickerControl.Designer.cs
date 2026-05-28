namespace Lspiguel.Xrm.D365ContextExporter.UI
{
    partial class SpecPickerControl
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
            this.components = new System.ComponentModel.Container();
            this.lblSpec = new System.Windows.Forms.Label();
            this.tableLayout = new System.Windows.Forms.TableLayoutPanel();
            this.cmbSpecs = new System.Windows.Forms.ComboBox();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayout.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblSpec
            // 
            this.lblSpec.AutoSize = true;
            this.lblSpec.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblSpec.Location = new System.Drawing.Point(0, 0);
            this.lblSpec.Name = "lblSpec";
            this.lblSpec.Size = new System.Drawing.Size(35, 13);
            this.lblSpec.TabIndex = 0;
            this.lblSpec.Text = "Spec:";
            // 
            // tableLayout
            // 
            this.tableLayout.ColumnCount = 2;
            this.tableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 27F));
            this.tableLayout.Controls.Add(this.cmbSpecs, 0, 0);
            this.tableLayout.Controls.Add(this.btnRefresh, 1, 0);
            this.tableLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayout.Location = new System.Drawing.Point(0, 13);
            this.tableLayout.Name = "tableLayout";
            this.tableLayout.RowCount = 1;
            this.tableLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayout.Size = new System.Drawing.Size(343, 30);
            this.tableLayout.TabIndex = 1;
            // 
            // cmbSpecs
            // 
            this.cmbSpecs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cmbSpecs.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSpecs.FormattingEnabled = true;
            this.cmbSpecs.Location = new System.Drawing.Point(3, 3);
            this.cmbSpecs.Name = "cmbSpecs";
            this.cmbSpecs.Size = new System.Drawing.Size(310, 21);
            this.cmbSpecs.TabIndex = 0;
            this.cmbSpecs.SelectedIndexChanged += new System.EventHandler(this.cmbSpecs_SelectedIndexChanged);
            // 
            // btnRefresh
            // 
            this.btnRefresh.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnRefresh.Location = new System.Drawing.Point(319, 3);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(21, 24);
            this.btnRefresh.TabIndex = 1;
            this.btnRefresh.Text = "↺";
            this.toolTip.SetToolTip(this.btnRefresh, "Refresh spec list");
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            // 
            // SpecPickerControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayout);
            this.Controls.Add(this.lblSpec);
            this.Name = "SpecPickerControl";
            this.Size = new System.Drawing.Size(343, 43);
            this.tableLayout.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.Label lblSpec;
        private System.Windows.Forms.TableLayoutPanel tableLayout;
        private System.Windows.Forms.ComboBox cmbSpecs;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.ToolTip toolTip;
    }
}
