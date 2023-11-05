namespace Esatto.AppCoordination.DemoCoordinatedNotepad
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            menuStrip1 = new MenuStrip();
            tsmiFile = new ToolStripMenuItem();
            tsmiOpen = new ToolStripMenuItem();
            tbText = new TextBox();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { tsmiFile });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(800, 24);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // tsmiFile
            // 
            tsmiFile.DropDownItems.AddRange(new ToolStripItem[] { tsmiOpen });
            tsmiFile.Name = "tsmiFile";
            tsmiFile.Size = new Size(37, 20);
            tsmiFile.Text = "&File";
            // 
            // tsmiOpen
            // 
            tsmiOpen.Name = "tsmiOpen";
            tsmiOpen.ShortcutKeys = Keys.Control | Keys.O;
            tsmiOpen.Size = new Size(180, 22);
            tsmiOpen.Text = "&Open";
            tsmiOpen.Click += tsmiOpen_Click;
            // 
            // tbText
            // 
            tbText.Dock = DockStyle.Fill;
            tbText.Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point);
            tbText.Location = new Point(0, 24);
            tbText.Multiline = true;
            tbText.Name = "tbText";
            tbText.Size = new Size(800, 426);
            tbText.TabIndex = 1;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(tbText);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "Form1";
            Text = "Form1";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip menuStrip1;
        private ToolStripMenuItem tsmiFile;
        private ToolStripMenuItem tsmiOpen;
        private TextBox tbText;
    }
}