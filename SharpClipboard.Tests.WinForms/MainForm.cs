using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace SharpClipboard.Tests.WinForms;

public partial class MainForm : Form
{
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
        if (e.ContentType == Mycoshiro.Windows.Forms.SharpClipboard.ContentTypes.Text)
        {
            txtCopiedTexts.Text = sharpClipboard1.ClipboardText;

            // Alternatively, you can use:
            // ---------------------------
            // txtCopiedTexts.Text = (string)e.Content;
        }
        else if (e.ContentType == Mycoshiro.Windows.Forms.SharpClipboard.ContentTypes.Image)
        {
            pbCopiedImage.Image = sharpClipboard1.ClipboardImage;

            // Alternatively, you can use:
            // ---------------------------
            // pbCopiedImage.Image = (Image)e.Content;
        }
        else if (e.ContentType == Mycoshiro.Windows.Forms.SharpClipboard.ContentTypes.Files)
        {
            // Declare variable to add the list of copied files.
            var files = new List<string>();

            // Add all copied files to the declared variable.
            foreach (string file in sharpClipboard1.ClipboardFiles) files.Add(Path.GetFileName(file));

            Debug.WriteLine(sharpClipboard1.ClipboardFiles);

            // Add all copied files to the files ListBox.
            lstCopiedFiles.Items.Clear();
            lstCopiedFiles.Items.AddRange(files.ToArray());

            // Alternatively, you can use:
            // ---------------------------
            // lstCopiedFiles.Items.AddRange(((List<string>)e.Content).ToArray()));
        }
        else if (e.ContentType == Mycoshiro.Windows.Forms.SharpClipboard.ContentTypes.Other)
        {
            // Do something with 'e.Content' or alternatively
            // 'sharpClipboard1.ClipboardObject' property here...

            // A great example is when a user has copied an Outlook Mail item.
            // Such an item will be of a complex object-type that can be parsed and
            // examined using the 'Microsoft.Office.Interop.Outlook' namespace features.
            // See here: https://stackoverflow.com/questions/25375367/how-to-copy-mailitem-in-outlook-c-sharp

            // You can however still use the 'ClipboardText' property if you
            // prefer simply displaying the copied object in text format.
            txtCopiedTexts.Text = sharpClipboard1.ClipboardText;
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
