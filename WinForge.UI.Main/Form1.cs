using WeifenLuo.WinFormsUI.Docking;
namespace WinForge.UI.Main
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        public void InitializeDockPanel()
        {
            var theme = new VS2015DarkTheme();
            this.dockPanel.Theme = theme;
        }
    }
}
