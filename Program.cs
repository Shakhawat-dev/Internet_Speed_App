using System.Windows.Forms;
using InternetSpeedApp;

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

using var window = new SpeedWindow();
window.Show();
Application.Run();
