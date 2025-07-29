using WeifenLuo.WinFormsUI.Docking;
using WinForge.Common;
using WinForge.IPC;
namespace WinForge.UI.Main
{
    public partial class MainForm : Form
    {
        public static UIMain UIMainInstance;
        public MainForm()
        {
            InitializeComponent();
            InitializeDockPanel();
            InitializeTrayIcon();
        }
        public void InitializeDockPanel()
        {
            var theme = new VS2015DarkTheme();
            this.dockPanel.Theme = theme;
        }
        private void InitializeTrayIcon()
        {
            notifyIcon = new NotifyIcon
            {
                Icon = new Icon("Icon.ico"), // Must be a .ico file
                Text = "WinForge",
                Visible = true
            };

            // Optional context menu
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Open", null, (s, e) => ShowMainForm());
            contextMenu.Items.Add("Exit", null, (s, e) => Stop());

            notifyIcon.ContextMenuStrip = contextMenu;

            notifyIcon.DoubleClick += (s, e) => ShowMainForm();
        }
        private void ShowMainForm()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
        }
        private void Stop()
        {
            UIMain.Communication.SendMessageAsync(new IPCMessage("WinForge.Base", UIMain.Communication.PipeName, "stop", IPCMessageType.Command));
            notifyIcon.Dispose();
            Application.Exit();
        }
        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
            }
        }
        public static void Init(DependencyService dependencyService)
        {
            UIMainInstance = new UIMain(dependencyService);
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
        }
        #region MenuStrip Items
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutForm form = new AboutForm();
            form.Show(dockPanel, DockState.Document);
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Stop();
        }
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Settings.Application.CloseToTray && e.CloseReason == CloseReason.UserClosing)
            {
                this.WindowState = FormWindowState.Minimized;
                e.Cancel = true;
            }
            else Stop();
        }
        #endregion
    }
}
