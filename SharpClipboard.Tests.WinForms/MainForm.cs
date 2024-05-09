using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using JetBrains.Annotations;
using static Mycoshiro.Windows.Forms.SharpClipboard;

namespace SharpClipboard.Tests.WinForms;

/// <inheritdoc />
[PublicAPI]
public sealed class SharpClipboardInvalidTypeException : Exception
{
    /// <summary>
    /// 
    /// </summary>
    public SharpClipboardInvalidTypeException() : base($"Invalid content type: {ContentTypes.Other}")
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="contentType"></param>
    public SharpClipboardInvalidTypeException(ContentTypes contentType) : base($"Invalid content type: {contentType}")
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    public SharpClipboardInvalidTypeException([CanBeNull] string message)
        : base(message)
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    /// <param name="inner"></param>
    public SharpClipboardInvalidTypeException([CanBeNull] string message, [CanBeNull] Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>
/// Main form used for testing the SharpClipboard library.      
/// </summary>
internal sealed partial class MainForm : Form
{
    /// <summary>
    /// Initialize new instance of the main form.
    /// </summary>
    public MainForm()
    {
        InitializeComponent();
    }

    private void sharpClipboard1_MonitorClipboardChanged(object sender, EventArgs e)
    {
        chkMonitorClipboard.Checked = sharpClipboard1.MonitorClipboard;
    }

    private void checkBox1_CheckedChanged(object sender, EventArgs e)
    {
        sharpClipboard1.MonitorClipboard = chkMonitorClipboard.Checked;
    }

    private void chkObserveTexts_CheckedChanged(object sender, EventArgs e)
    {
        sharpClipboard1.ObservableFormats.Texts = chkObserveTexts.Checked;
    }

    private void chkObserveImages_CheckedChanged(object sender, EventArgs e)
    {
        sharpClipboard1.ObservableFormats.Images = chkObserveImages.Checked;
    }

    private void chkObserveFiles_CheckedChanged(object sender, EventArgs e)
    {
        sharpClipboard1.ObservableFormats.Files = chkObserveFiles.Checked;
    }

    private void sharpClipboard1_ClipboardChanged(object sender,
        Mycoshiro.Windows.Forms.ClipboardChangedEventArgs e)
    {
        switch (e.ContentType)
        {
            case Mycoshiro.Windows.Forms.SharpClipboard.ContentTypes.Text:
                txtCopiedTexts.Text = sharpClipboard1.ClipboardText ?? string.Empty;

                // Alternatively, you can use:
                // ---------------------------
                // txtCopiedTexts.Text = (string)e.Content;
                break;
            case Mycoshiro.Windows.Forms.SharpClipboard.ContentTypes.Image:
                pbCopiedImage.Image = sharpClipboard1.ClipboardImage;

                // Alternatively, you can use:
                // ---------------------------
                // pbCopiedImage.Image = (Image)e.Content;
                break;
            case Mycoshiro.Windows.Forms.SharpClipboard.ContentTypes.Files:
                // Declare variable to add the list of copied files.

                // Add all copied files to the declared variable.

                Debug.WriteLine(sharpClipboard1.ClipboardFiles);

                // Add all copied files to the files ListBox.
                lstCopiedFiles.Items.Clear();
                lstCopiedFiles.Items.AddRange(sharpClipboard1.ClipboardFiles.Select(Path.GetFileName) as object[] ?? []);

                // Alternatively, you can use:
                // ---------------------------
                // lstCopiedFiles.Items.AddRange(((List<string>)e.Content).ToArray()));
                break;
            case Mycoshiro.Windows.Forms.SharpClipboard.ContentTypes.Other:
                // Do something with 'e.Content' or alternatively
                // 'sharpClipboard1.ClipboardObject' property here...

                // A great example is when a user has copied an Outlook Mail item.
                // Such an item will be of a complex object-type that can be parsed and
                // examined using the 'Microsoft.Office.Interop.Outlook' namespace features.
                // See here: https://stackoverflow.com/questions/25375367/how-to-copy-mailitem-in-outlook-c-sharp

                // You can however still use the 'ClipboardText' property if you
                // prefer simply displaying the copied object in text format.
                txtCopiedTexts.Text = sharpClipboard1.ClipboardText ?? string.Empty;
                break;
            default:
                throw new SharpClipboardInvalidTypeException(e.ContentType);
        }

        // If you wish to get details of the application from where
        // any text, file, image or other objects were cut/copied,
        // simply add a TextBox and uncomment the lines below.
        // --------------------------------------------------------
        // textBox1.Text =
        //     $"Name: {e.SourceApplication.Name} \n" +
        //     $"Title: {e.SourceApplication.Title} \n" +
        //     $"Id: {e.SourceApplication.Id} \n" +
        //     $"Handle: {e.SourceApplication.Handle} \n" +
        //     $"Path: {e.SourceApplication.Path}";
        // --------------------------------------------------------
        // This could come in-handy if you're developing a clipboard-monitoring app.
    }
}
