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
        private RegistryKey osUpgrade = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\OSUpgrade", true);

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
            this.Topmost = true;
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
                        win10UpgradeCheckbox.IsOn = Convert.ToBoolean(osUpgrade.GetValue("AllowOSUpgrade"));
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

        private async void connectButton_Click(object sender, RoutedEventArgs e)
        {
            // Update UI
            progressRing.IsActive = true;
            statusText.Content = "Connecting";
            statusText.Foreground = Brushes.Gray;
            connectButton.IsEnabled = false;
            connectButton.SetResourceReference(StyleProperty, "DefaultButtonStyle");

            await Task.Delay(500);

            // Show warning window
            WarningWindow warningWindow = new WarningWindow();
            warningWindow.ShowDialog();

            if (warningWindow.termsCheckbox.IsChecked ?? false == true)
            {
                // Registry changes needed to make this work
                updatePoliciesKey.SetValue("WUServer", Settings.FQDN);
                updatePoliciesKey.SetValue("WUStatusServer", Settings.FQDN);
                updatePoliciesKey.SetValue("DoNotConnectToWindowsUpdateInternetLocations", 1);
                updatePoliciesKey.CreateSubKey("AU", true).SetValue("UseWUServer", 1);
                updatePoliciesKey.CreateSubKey("AU", true).SetValue("AUOptions", 4);
                updatePoliciesKey.Flush();

                // Restart Windows Update service
                RestartUpdateService();

                // Launch Windows Update page on UWP Settings/Control Panel
                ProcessStartInfo openWindowsUpdatePage = new ProcessStartInfo()
                {
                    FileName = "control.exe",
                    Arguments = "update"
                };
                Process.Start(openWindowsUpdatePage);

                // Tell Windows Update to check for updates now
                ProcessStartInfo checkForUpdates = new ProcessStartInfo();
                if (operatingSystemLabel.Content.ToString().Contains("Windows 10") || operatingSystemLabel.Content.ToString().Contains("Windows 11"))
                {
                    checkForUpdates.FileName = "UsoClient.exe";
                    checkForUpdates.Arguments = "StartInteractiveScan";
                }
                else
                {
                    checkForUpdates.FileName = "wuauclt.exe";
                    checkForUpdates.Arguments = "/resetauthorization /detectnow /updatenow /reportnow";
                    win10UpgradeCheckbox.IsEnabled = true;
                }
                Process.Start(checkForUpdates).WaitForExit();

                // Update UI
                disconnectButton.IsEnabled = true;
                disconnectButton.SetResourceReference(StyleProperty, "AccentButtonStyle");
                statusText.Content = "Using SGBono WSUS server";
                statusText.Foreground = Brushes.LightGreen;
                reminderWarning.Visibility = Visibility.Visible;
            } else
            {
                // Revert UI
                connectButton.IsEnabled = true;
                connectButton.SetResourceReference(StyleProperty, "AccentButtonStyle");
                statusText.Content = "Using Windows Update servers";
                statusText.Foreground = Brushes.Red;
            }

            // Update UI
            progressRing.IsActive = false;
        }

        private void disconnectButton_Click(object sender, RoutedEventArgs e)
        {
            // Update UI
            progressRing.IsActive = true;
            statusText.Content = "Disconnecting";
            statusText.Foreground = Brushes.Gray;
            win10UpgradeCheckbox.IsEnabled = false;
            disconnectButton.IsEnabled = false;
            disconnectButton.SetResourceReference(StyleProperty, "DefaultButtonStyle");

            // Registry changes needed to make this work
            updatePoliciesKey.DeleteSubKeyTree("AU");
            foreach (var key in updatePoliciesKey.GetValueNames())
            {
                updatePoliciesKey.DeleteValue(key);
            }

            // Restart Windows Update service
            RestartUpdateService();

            // Update UI
            connectButton.IsEnabled = true;
            connectButton.SetResourceReference(StyleProperty, "AccentButtonStyle");
            statusText.Content = "Using Windows Update servers";
            statusText.Foreground = Brushes.Red;
            reminderWarning.Visibility = Visibility.Collapsed;
            progressRing.IsActive = false;
        }

        private void RestartUpdateService()
        {
            ProcessStartInfo stopWuauserv = new ProcessStartInfo()
            {
                FileName = "net.exe",
                Arguments = "stop wuauserv",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };
            Process.Start(stopWuauserv).WaitForExit();

            ProcessStartInfo startWuauserv = new ProcessStartInfo()
            {
                FileName = "net.exe",
                Arguments = "start wuauserv",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };
            Process.Start(startWuauserv).WaitForExit();
        }

        private void win10UpgradeCheckbox_Toggled(object sender, RoutedEventArgs e)
        {
            if (win10UpgradeCheckbox.IsOn)
            {
                osUpgrade.SetValue("AllowOSUpgrade", 1);
            }
            else
            {
                osUpgrade.DeleteValue("AllowOSUpgrade");
            }
        }

        private void alwaysOnTopCheckbox_Toggled(object sender, RoutedEventArgs e)
        {
            this.Topmost = alwaysOnTopCheckbox.IsOn;
        }
    }
}