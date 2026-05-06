namespace Lspiguel.Xrm.D365ContextExporter
{
    partial class ContextExporterPluginControl
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
            this.dirPicker = new Lspiguel.Xrm.D365ContextExporter.UI.BaseDirectoryPickerControl();
            this.specPicker = new Lspiguel.Xrm.D365ContextExporter.UI.SpecPickerControl();
            this.toolbarPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.btnRun = new System.Windows.Forms.Button();
            this.progressControl = new Lspiguel.Xrm.D365ContextExporter.UI.ExportProgressControl();
            this.outputPreview = new Lspiguel.Xrm.D365ContextExporter.UI.OutputPreviewControl();
            this.mainLayout.SuspendLayout();
            this.toolbarPanel.SuspendLayout();
            this.SuspendLayout();

            // mainLayout
            this.mainLayout.ColumnCount = 1;
            this.mainLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainLayout.Controls.Add(this.dirPicker, 0, 0);
            this.mainLayout.Controls.Add(this.specPicker, 0, 1);
            this.mainLayout.Controls.Add(this.toolbarPanel, 0, 2);
            this.mainLayout.Controls.Add(this.progressControl, 0, 3);
            this.mainLayout.Controls.Add(this.outputPreview, 0, 4);
            this.mainLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainLayout.Location = new System.Drawing.Point(8, 8);
            this.mainLayout.Name = "mainLayout";
            this.mainLayout.RowCount = 5;
            this.mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.mainLayout.Size = new System.Drawing.Size(784, 561);
            this.mainLayout.TabIndex = 0;

            // dirPicker
            this.dirPicker.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dirPicker.Location = new System.Drawing.Point(3, 3);
            this.dirPicker.Name = "dirPicker";
            this.dirPicker.Size = new System.Drawing.Size(778, 50);
            this.dirPicker.TabIndex = 0;
            this.dirPicker.DirectoryChanged += new System.EventHandler<string>(this.dirPicker_DirectoryChanged);

            // specPicker
            this.specPicker.Dock = System.Windows.Forms.DockStyle.Fill;
            this.specPicker.Location = new System.Drawing.Point(3, 59);
            this.specPicker.Name = "specPicker";
            this.specPicker.Size = new System.Drawing.Size(778, 50);
            this.specPicker.TabIndex = 1;
            this.specPicker.SpecSelected += new System.EventHandler<Lspiguel.Xrm.D365ContextExporter.Models.ExportJob?>(this.specPicker_SpecSelected);

            // toolbarPanel
            this.toolbarPanel.AutoSize = true;
            this.toolbarPanel.Controls.Add(this.btnRun);
            this.toolbarPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.toolbarPanel.Location = new System.Drawing.Point(3, 115);
            this.toolbarPanel.Name = "toolbarPanel";
            this.toolbarPanel.Size = new System.Drawing.Size(778, 35);
            this.toolbarPanel.TabIndex = 2;

            // btnRun
            this.btnRun.Enabled = false;
            this.btnRun.Location = new System.Drawing.Point(3, 3);
            this.btnRun.Name = "btnRun";
            this.btnRun.Size = new System.Drawing.Size(100, 28);
            this.btnRun.TabIndex = 0;
            this.btnRun.Text = "Run Export";
            this.btnRun.UseVisualStyleBackColor = true;
            this.btnRun.Click += new System.EventHandler(this.btnRun_Click);

            // progressControl
            this.progressControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.progressControl.Location = new System.Drawing.Point(3, 156);
            this.progressControl.Name = "progressControl";
            this.progressControl.Size = new System.Drawing.Size(778, 272);
            this.progressControl.TabIndex = 3;
            this.progressControl.CancelRequested += new System.EventHandler(this.progressControl_CancelRequested);

            // outputPreview
            this.outputPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.outputPreview.Location = new System.Drawing.Point(3, 434);
            this.outputPreview.Name = "outputPreview";
            this.outputPreview.Size = new System.Drawing.Size(778, 130);
            this.outputPreview.TabIndex = 4;
            this.outputPreview.Visible = false;

            // ContextExporterPluginControl
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.mainLayout);
            this.Name = "ContextExporterPluginControl";
            this.Padding = new System.Windows.Forms.Padding(8);
            this.Size = new System.Drawing.Size(800, 577);
            this.Load += new System.EventHandler(this.ContextExporterPluginControl_Load);
            this.VisibleChanged += new System.EventHandler(this.ContextExporterPluginControl_VisibleChanged);
            this.mainLayout.ResumeLayout(false);
            this.mainLayout.PerformLayout();
            this.toolbarPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.TableLayoutPanel mainLayout;
        private Lspiguel.Xrm.D365ContextExporter.UI.BaseDirectoryPickerControl dirPicker;
        private Lspiguel.Xrm.D365ContextExporter.UI.SpecPickerControl specPicker;
        private System.Windows.Forms.FlowLayoutPanel toolbarPanel;
        private System.Windows.Forms.Button btnRun;
        private Lspiguel.Xrm.D365ContextExporter.UI.ExportProgressControl progressControl;
        private Lspiguel.Xrm.D365ContextExporter.UI.OutputPreviewControl outputPreview;
    }
}
