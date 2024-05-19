using System;
using System.Windows.Forms;
using JetBrains.Annotations;

namespace SharpClipboard.Tests.WinForms
{
    /// <summary>
    /// Default program class.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [PublicAPI]
        [STAThread]
        public static void Main()
        {
            _ = Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            MainForm form = null;
            try
            {
                form = new MainForm();
                Application.Run(form);
            }
            finally
            {
                form?.Dispose();
            }
        }
    }
}
