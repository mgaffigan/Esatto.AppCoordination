namespace Esatto.AppCoordination.DemoCoordinatedNotepad
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        public void AcceptArgs(string[] args)
        {
            if (IsHandleCreated)
            {
                Activate();
            }

            if (args.Length > 0)
            {
                Open(args[0]);
            }
        }

        private void tsmiOpen_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
            if (ofd.ShowDialog(this) == DialogResult.Cancel)
            {
                return;
            }

            Open(ofd.FileName);
        }

        public void Open(string fileName)
        {
            var text = File.ReadAllText(fileName);
            var name = Path.GetFileName(fileName);

            tbText.Text = text;
            this.Text = name + " - CoordinatedNotepad";
        }
    }
}