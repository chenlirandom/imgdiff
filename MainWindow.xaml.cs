using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace imgdiff
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        int tolerance = 0;
        string leftFile = null;
        string rightFile = null;

        public MainWindow()
        {
            InitializeComponent();
            int tol;
            if (int.TryParse(Environment.GetEnvironmentVariable("IMAGE_DIFF_DEFAULT_TOLERANCE"), out tol))
            {
                this.tolerance = Math.Max(0, Math.Min(255, tol));
            }
            this.Loaded += (s, e) => { Startup(); };
        }

        public void ErrorLogLine(string message)
        {
            this.imageDiff.Visibility = Visibility.Collapsed;
            this.logText.Visibility = Visibility.Visible;
            this.logText.Text = this.logText.Text + message + "\r\n";
        }

        void Startup()
        {
            if (!ParseCommandLine(Environment.GetCommandLineArgs()))
            {
                return;
            }

            var leftImage = OpenImageFile(this.leftFile);
            if (null == leftImage) return;

            var rightImage = OpenImageFile(this.rightFile);
            if (null == rightImage) return;

            this.imageDiff.SetImages(leftImage, rightImage, this.tolerance);
        }

        BitmapImage OpenImageFile(string path)
        {
            if (!File.Exists(path))
            {
                ErrorLogLine(string.Format("File not found: {0}", path));
                return null;
            }
            try
            {
                return new BitmapImage(new Uri(Path.GetFullPath(path)));
            }
            catch(Exception ex)
            {
                ErrorLogLine(string.Format("Fail to open file {0}: {1}", path, ex.ToString()));
                return null;
            }
        }

        bool ParseCommandLine(string[] args)
        {
            if (args.Length < 2)
            {
                ShowUsage();
                return false;
            }

            for (int i = 1; i < args.Length; ++i)
            {
                var a = args[i];

                if (a[0] == '-' || a[0] == '/')
                {
                    string o = a.Substring(1).ToLower();
                    if ("h" == o || "?" == o)
                    {
                        ShowUsage();
                        return false;
                    }
                    else if ("t" == o)
                    {
                        if ((i+1) < args.Length)
                        {
                            ++i;
                            if (!int.TryParse(args[i], out this.tolerance))
                            {
                                ErrorLogLine("Tolerance value must be an integer.");
                                return false;
                            }
                        }
                        else
                        {
                            ErrorLogLine("Missing tolerance number.");
                            return false;
                        }
                    }

                }
                else if (string.IsNullOrEmpty(this.leftFile))
                {
                    this.leftFile = a;
                }
                else if (string.IsNullOrEmpty(this.rightFile))
                {
                    this.rightFile = a;
                }
            }

            if (string.IsNullOrEmpty(leftFile))
            {
                ErrorLogLine("Left file is not defined.");
                return false;
            }

            if (string.IsNullOrEmpty(rightFile))
            {
                ErrorLogLine("Right file is not defined.");
                return false;
            }

            return true;
        }

        void ShowUsage()
        {
            string usage = @"
Usage: [options] <left> <right>

Options:
    -t [0-255]  Set image diff tolerance. Default is 0, or whatever defined
                by environment variable IMAGE_DIFF_DEFAULT_TOLERANCE.
";
            ErrorLogLine(usage);
        }
    }
}
