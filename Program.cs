using System.Windows.Forms;

namespace InternetSpeedApp;

internal static class Program
{
    // STA is required by WinForms COM-based dialogs (SaveFileDialog, ColorDialog);
    // without it the dialog call deadlocks the UI thread.
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        using var window = new SpeedWindow();
        window.Show();
        Application.Run();
    }
}
