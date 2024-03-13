/*
 * The SharpClipboard Handle.
 * ---------------------------------------------+
 * Please preserve this window.
 * It acts as the core message-processing handle
 * for receiving broadcasted clipboard messages.
 *
 * The window however will not be visible to
 * end users both via the Taskbar and the Task-
 * Manager so no need to panic. At the very least
 * you may change the window's title using the
 * static property 'SharpClipboard.HandleCaption'.
 * ---------------------------------------------+
 *
 */

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using JetBrains.Annotations;

namespace WK.Libraries.SharpClipboardNS.Views;

/// <summary>
/// This window acts as a handle to the clipboard-monitoring process and
/// thus will be launched in the background once the component has started
/// the monitoring service. However, it won't be visible to anyone even via
/// the Task Manager.
/// </summary>
public sealed partial class ClipboardHandle : Form
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private const int WM_DRAWCLIPBOARD = 0x0308;

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private const int WM_CHANGECBCHAIN = 0x030D;

    /// <summary>
    /// Gets or sets an active <see cref="SharpClipboard" /> instance
    /// for use when managing the current clipboard handle.
    /// </summary>
    [Browsable(false)] private readonly SharpClipboard _sharpClipboardInstance;

    private IntPtr _chainedWnd;
    private string? _executableName;
    private string? _executablePath;
    private string? _processName;

    private bool _ready;

    /// <summary>
    /// Initializes a new instance of <see cref="ClipboardHandle" />.
    /// </summary>
    [SupportedOSPlatform("windows6.0")]
    public ClipboardHandle(SharpClipboard instance)
    {
        _sharpClipboardInstance = instance;

        InitializeComponent();

        // [optional] Applies the default window title.
        // This may only be necessary for forensic purposes.
        Text = SharpClipboard.HandleCaption;
    }

    /// <summary>
    /// Checks if the handle is ready to monitor the system clipboard.
    /// It is used to provide a final value for use whenever the property
    /// 'ObserveLastEntry' is enabled.
    /// </summary>
    [Browsable(false)]
    private bool Ready
    {
        get
        {
            if (_sharpClipboardInstance.ObserveLastEntry)
            {
                _ready = true;
            }

            return _ready;
        }
        set => _ready = value;
    }

    /// <summary>
    /// Modifications in this overriden method have
    /// been added to disable viewing of the handle-
    /// window in the Task Manager.
    /// </summary>
    [SupportedOSPlatform("windows6.0")]
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;

            // Turn on WS_EX_TOOLWINDOW.
            cp.ExStyle |= 0x80;

            return cp;
        }
    }

    /// <summary>
    /// Return the Process Name of the current active window.
    /// </summary>
    [PublicAPI]
    public string? ProcessName
    {
        get
        {
            GetApplicationName();
            return _processName;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);

    [DllImport("user32.dll")]
    private static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// This is the main clipboard detection method.
    /// Algorithmic customizations are most welcome.
    /// </summary>
    /// <param name="m">The processed window-reference message.</param>
    [SupportedOSPlatform("windows")]
    [SuppressMessage("ReSharper", "CognitiveComplexity")]
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        switch (m.Msg)
        {
            case WM_DRAWCLIPBOARD:

                // If clipboard-monitoring is enabled, proceed to listening.
                if (Ready && _sharpClipboardInstance.MonitorClipboard)
                {
                    IDataObject? dataObj;
                    int retryCount = 0;

                    while (true)
                    {
                        try
                        {
                            dataObj = Clipboard.GetDataObject();
                            break;
                        }
                        catch (ExternalException)
                        {
                            // Crashes when large data is copied from clipboard
                            // without retries. We'll therefore need to do a 5-step
                            // retry count to cut some slack for the operation to
                            // fully complete and ensure that the data is captured;
                            // if all else fails, then throw an exception.
                            // You may extend the retries if need be.
                            if (++retryCount > 5)
                            {
                                throw;
                            }

                            Thread.Sleep(100);
                        }
                    }

                    try
                    {
                        // Determines whether a file/files have been cut/copied.
                        if (_sharpClipboardInstance.ObservableFormats.Files &&
                            dataObj?.GetDataPresent(DataFormats.FileDrop) != null &&
                            dataObj.GetData(DataFormats.FileDrop) is string?[] capturedFiles)
                        {
                            // If the 'capturedFiles' string array persists as null, then this means
                            // that the copied content is of a complex object type since the file-drop
                            // format is able to capture more-than-just-file content in the clipboard.
                            // Therefore assign the content its rightful type.
                            if (capturedFiles == null)
                            {
                                _sharpClipboardInstance.ClipboardObject = dataObj;
                                _sharpClipboardInstance.ClipboardText =
                                    dataObj.GetData(DataFormats.UnicodeText)?.ToString() ?? string.Empty;

                                _sharpClipboardInstance.Invoke(
                                    dataObj, 
                                    SharpClipboard.ContentTypes.Other,
                                    new SourceApplication(
                                        GetForegroundWindow(),
                                        SharpClipboard.ForegroundWindowHandle(),
                                        GetApplicationName(),
                                        GetActiveWindowTitle(),
                                        GetApplicationPath()));
                            }
                            else
                            {
                                // Clear all existing files before update.
                                _sharpClipboardInstance.ClipboardFiles.Clear();

                                _sharpClipboardInstance.ClipboardFiles.AddRange(capturedFiles);
                                _sharpClipboardInstance.ClipboardFile = capturedFiles[0];

                                _sharpClipboardInstance.Invoke(capturedFiles, SharpClipboard.ContentTypes.Files,
                                    new SourceApplication(GetForegroundWindow(),
                                        SharpClipboard.ForegroundWindowHandle(),
                                        GetApplicationName(), GetActiveWindowTitle(), GetApplicationPath()));
                            }
                        }

                        // Determines whether text has been cut/copied.
                        else if (_sharpClipboardInstance.ObservableFormats.Texts &&
                                 (dataObj?.GetDataPresent(DataFormats.Text) ??
                                  dataObj?.GetDataPresent(DataFormats.UnicodeText)) != null)
                        {
                            string? capturedText = dataObj?.GetData(DataFormats.UnicodeText)?.ToString();
                            _sharpClipboardInstance.ClipboardText = capturedText;
                            _sharpClipboardInstance.Invoke(capturedText, SharpClipboard.ContentTypes.Text,
                                new SourceApplication(GetForegroundWindow(), SharpClipboard.ForegroundWindowHandle(),
                                    GetApplicationName(), GetActiveWindowTitle(), GetApplicationPath()));
                        }

                        // Determines whether an image has been cut/copied.
                        else if (_sharpClipboardInstance.ObservableFormats.Images &&
                                 dataObj?.GetDataPresent(DataFormats.Bitmap) != null)
                        {
                            Image? capturedImage = dataObj.GetData(DataFormats.Bitmap) as Image;
                            _sharpClipboardInstance.ClipboardImage = capturedImage;

                            _sharpClipboardInstance.Invoke(capturedImage, SharpClipboard.ContentTypes.Image,
                                new SourceApplication(GetForegroundWindow(), SharpClipboard.ForegroundWindowHandle(),
                                    GetApplicationName(), GetActiveWindowTitle(), GetApplicationPath()));
                        }

                        // Determines whether a complex object has been cut/copied.
                        else if (_sharpClipboardInstance.ObservableFormats.Others &&
                                 dataObj?.GetDataPresent(DataFormats.FileDrop) != null)
                        {
                            _sharpClipboardInstance.Invoke(dataObj, SharpClipboard.ContentTypes.Other,
                                new SourceApplication(GetForegroundWindow(), SharpClipboard.ForegroundWindowHandle(),
                                    GetApplicationName(), GetActiveWindowTitle(), GetApplicationPath()));
                        }
                    }
                    catch (AccessViolationException)
                    {
                        // Use-cases such as Remote Desktop usage might throw this exception.
                        // Applications with Administrative privileges can however override
                        // this exception when run in a production environment.
                    }
                    catch (NullReferenceException)
                    {
                    }
                }

                // Provides support for multi-instance clipboard monitoring.
                SendMessage(_chainedWnd, m.Msg, m.WParam, m.LParam);

                break;

            case WM_CHANGECBCHAIN:

                if (m.WParam == _chainedWnd)
                {
                    _chainedWnd = m.LParam;
                }
                else
                {
                    SendMessage(_chainedWnd, m.Msg, m.WParam, m.LParam);
                }

                break;
        }
    }

    /// <summary>
    /// Start monitoring the clipboard.
    /// </summary>
    [SupportedOSPlatform("windows6.0")]
    public void StartMonitoring()
    {
        Show();
    }

    /// <summary>
    /// Stop monitoring the clipboard.
    /// </summary>
    [SupportedOSPlatform("windows6.0")]
    public void StopMonitoring()
    {
        Close();
    }

    [DllImport("user32.dll")]
    private static extern int GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindowPtr();

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32")]
    private static extern uint GetWindowThreadProcessId(int hWnd, out int lpdwProcessId);

    private static int GetProcessID(int hwnd)
    {
        _ = GetWindowThreadProcessId(hwnd, out int processID);

        return processID;
    }

    private string? GetApplicationName()
    {
        try
        {
            int hwnd = GetForegroundWindow();

            _processName = Process.GetProcessById(GetProcessID(hwnd)).ProcessName;
            _executablePath = Process.GetProcessById(GetProcessID(hwnd)).MainModule?.FileName;
            _executableName = _executablePath?[(_executablePath.LastIndexOf('\\') + 1)..];
        }
        catch (Exception)
        {
        }

        return _executableName;
    }

    private string? GetApplicationPath()
    {
        return _executablePath;
    }

    private static string? GetActiveWindowTitle()
    {
        const int capacity = 256;
        try
        {
            StringBuilder? content = new(capacity);
            nint handle = SharpClipboard.ForegroundWindowHandle();
            if (GetWindowText(handle, content, capacity) > 0)
            {
                return content.ToString();
            }
        }
        catch (Exception)
        {
        }


        return null;
    }

    [SupportedOSPlatform("windows6.0")]
    private void OnLoad(object sender, EventArgs e)
    {
        // Start listening for clipboard changes.
        _chainedWnd = SetClipboardViewer(Handle);

        Ready = true;
    }

    [SupportedOSPlatform("windows6.0")]
    private void OnClose(object sender, FormClosingEventArgs e)
    {
        // Stop listening to clipboard changes.
        ChangeClipboardChain(Handle, _chainedWnd);

        _chainedWnd = IntPtr.Zero;
    }
}