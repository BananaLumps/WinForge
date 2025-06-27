namespace WinForge.Core
{
    public partial class UICore : Form
    {
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;

        public UICore()
        {
            InitializeComponent();

            // Create a context menu for the tray icon
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Open", null, (s, e) => ShowFromTray());
            trayMenu.Items.Add("Exit", null, (s, e) => Application.Exit());

            // Create the tray icon
            trayIcon = new NotifyIcon
            {
                Text = "WinForge",
                Icon = SystemIcons.Application, // You can set your own icon here
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            trayIcon.DoubleClick += (s, e) => ShowFromTray();

            // Start minimized and hidden
            MinimizeToTray();
        }

        private void MinimizeToTray()
        {
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Hide();
        }

        private void ShowFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.BringToFront();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
            base.OnFormClosing(e);
        }
    }
}
