using System;
using System.Windows.Forms;
using JetBrains.Annotations;

namespace SharpClipboard.Tests.WinForms
{
    /// <summary>
    /// Default program class.
    /// </summary>
    public static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [PublicAPI]
        [STAThread]
        public static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
