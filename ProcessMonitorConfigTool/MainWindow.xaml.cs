using HandyControl.Controls;
using System.Linq;

namespace ProcessMonitorConfigTool
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new MainWindowVM();
            WindowManager.Register<EditWindow>("EditWindow");
        }
    }
}
    