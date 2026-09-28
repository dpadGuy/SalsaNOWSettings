using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using SalsaNOWSettings.Helpers;

namespace SalsaNOWSettings
{
    public partial class App : Application
    {
        private Window? _window;

        public static Window? MainWindow { get; private set; }

        public App()
        {
            AppContext.SetSwitch("System.Runtime.InteropServices.BuiltInComInterop.IsSupported", true);
            AppContext.SetSwitch("System.Runtime.InteropServices.Marshalling.EnableGeneratedComInterfaceComImportInterop", true);
            InitializeComponent();
            UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += (_, e) => e.SetObserved();
        }

        private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            SalsaNOWConfig.EnsureExists();
            _window = new MainWindow();
            MainWindow = _window;
            _window.Activate();
            WindowsColorSettings.ApplyFromConfig();
            WindowsTaskbarSettings.ApplyFromConfig();
        }
    }
}
