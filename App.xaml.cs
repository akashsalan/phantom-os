using System.Windows;
using System.Windows.Threading;

namespace PhantomOS
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Prevent unhandled exceptions from crashing the app
            this.DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        private void OnDispatcherUnhandledException(object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            System.Windows.MessageBox.Show(e.Exception.ToString() + "\nInner: " + e.Exception.InnerException?.ToString(), "CRASH LOG", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = false;
        }
    }
}
