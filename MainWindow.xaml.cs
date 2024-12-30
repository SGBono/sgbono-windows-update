using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace sgbono_windows_update
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static XDocument settings = XDocument.Load(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + @"\Settings.xml");
        private RegistryKey updatePoliciesKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", true);

        private class Settings
        {
            public readonly static string ServerName = settings.Root.Element("WSUSServer").Value;
            public readonly static int Port = Convert.ToInt32(settings.Root.Element("WSUSPort").Value);
            public readonly static string Protocol = settings.Root.Element("WSUSUseSSL").Value == "True" ? "https" : "http";
            public readonly static string FQDN = $"{Protocol}://{ServerName}:{Port}";
        }

        public MainWindow()
        {
            InitializeComponent();
            iNKORE.UI.WPF.Modern.ThemeManager.Current.ApplicationTheme = iNKORE.UI.WPF.Modern.ApplicationTheme.Dark;

            // Check for Windows 10 and 11 Home - unsupported OSes
            Process checkWindowsVersion = new Process();
            checkWindowsVersion.StartInfo.FileName = "systeminfo.exe";
            checkWindowsVersion.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            checkWindowsVersion.StartInfo.UseShellExecute = false;
            checkWindowsVersion.StartInfo.RedirectStandardOutput = true;
            checkWindowsVersion.StartInfo.CreateNoWindow = true;
            checkWindowsVersion.Start();
            string output = checkWindowsVersion.StandardOutput.ReadToEnd();
            checkWindowsVersion.WaitForExit();
            
            string[] formattedOutput = output.Split('\n');
            foreach (string line in formattedOutput)
            {
                if (line.Contains("OS Name: "))
                {
                    string value = line.Replace("OS Name:", "").Trim();
                    if (value.Contains("Windows 11") || value.Contains("Windows 10")) 
                    {
                        if (value.Contains("Home"))
                        {
                            connectButton.IsEnabled = false;
                            disconnectButton.IsEnabled = false;
                            homeEditionError.Visibility = Visibility.Visible;
                        }
                    } else
                    {
                        win10UpgradeCheckbox.IsEnabled = true;
                    }

                    operatingSystemLabel.Content = $"OS: {value.Replace("Microsoft", "")}";
                }
            }

            // Check if Windows Update already points to SGBono WSUS servers
            if (Convert.ToInt32(updatePoliciesKey.CreateSubKey("AU")?.GetValue("UseWUServer") ?? 0) == 1 && 
                ((updatePoliciesKey?.GetValue("WUServer") ?? "").ToString() == Settings.FQDN))
            {
                statusText.Content = "Using SGBono WSUS servers";
                statusText.Foreground = Brushes.LightGreen;
                connectButton.IsEnabled = false;
                connectButton.SetResourceReference(StyleProperty, "DefaultButtonStyle");
                disconnectButton.IsEnabled = true;
                disconnectButton.SetResourceReference(StyleProperty, "AccentButtonStyle");
                reminderWarning.Visibility = Visibility.Visible;
            }
        }

        private void connectButton_Click(object sender, RoutedEventArgs e)
        {
            progressRing.IsActive = true;
            statusText.Content = "Connecting";
            statusText.Foreground = Brushes.Gray;
            connectButton.IsEnabled = false;
            connectButton.SetResourceReference(StyleProperty, "DefaultButtonStyle");

            updatePoliciesKey.SetValue("WUServer", Settings.FQDN);
            updatePoliciesKey.SetValue("WUStatusServer", Settings.FQDN);
            updatePoliciesKey.SetValue("DoNotConnectToWindowsUpdateInternetLocations", 1);
            updatePoliciesKey.CreateSubKey("AU").SetValue("UseWUServer", 1);

            disconnectButton.IsEnabled = true;
            disconnectButton.SetResourceReference(StyleProperty, "AccentButtonStyle");
            statusText.Content = "Using SGBono WSUS server";
            statusText.Foreground = Brushes.LightGreen;
            reminderWarning.Visibility = Visibility.Visible;
            progressRing.IsActive = false;
        }
    }
}