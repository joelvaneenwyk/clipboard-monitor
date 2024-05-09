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
using System.Threading;
using System.Windows.Forms;
using JetBrains.Annotations;

#pragma warning disable IDE0079

namespace Mycoshiro.Windows.Forms.Views;

/// <summary>
/// This window acts as a handle to the clipboard-monitoring process and
/// thus will be launched in the background once the component has started
/// the monitoring service. However, it won't be visible to anyone even via
/// the Task Manager.
/// </summary>
[SuppressMessage("Usage", "CA2216:Disposable types should declare finalizer", Justification = "Not necessary in this case.")]
[MustDisposeResource]
[SuppressMessage("ReSharper", "MissingSuppressionJustification")]
public sealed partial class ClipboardHandle : Form
{
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Matches the Windows API")]
    private const int WM_DRAWCLIPBOARD = 0x0308;

    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Matches the Windows API")]
    private const int WM_CHANGECBCHAIN = 0x030D;

    /// <summary>
    /// Gets or sets an active <see cref="SharpClipboard" /> instance
    /// for use when managing the current clipboard handle.
    /// </summary>
    [Browsable(false)]
    private readonly SharpClipboard _sharpClipboardInstance;

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
    /// Modifications in this overridden method have been added to disable
    /// viewing of the handle-window in the Task Manager.
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
            _ = GetApplicationName();
            return _processName;
        }
    }

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial IntPtr SetClipboardViewer(IntPtr hWndNewViewer);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    private static IDataObject? GetClipboardDataObject()
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

        return dataObj;
    }

    private void SetClipboardFiles(IDataObject? dataObj)
    {
        // Determines whether a file/files have been cut/copied.
        if (_sharpClipboardInstance.ObservableFormats.Files &&
            dataObj?.GetDataPresent(DataFormats.FileDrop) != null)
        {
            if (dataObj.GetData(DataFormats.FileDrop) is string?[] capturedFiles)
            {
                // Clear all existing files before update.
                _sharpClipboardInstance.SetClipboardFiles(capturedFiles);
                _sharpClipboardInstance.ClipboardFile = capturedFiles[0];

                _sharpClipboardInstance.Invoke(capturedFiles, SharpClipboard.ContentTypes.Files,
                    new SourceApplication(GetForegroundWindow(),
                        SharpClipboard.ForegroundWindowHandle(),
                        GetApplicationName(), GetActiveWindowTitle(), GetApplicationPath()));
            }
            // If the 'capturedFiles' string array persists as null, then this means
            // that the copied content is of a complex object type since the file-drop
            // format is able to capture more-than-just-file content in the clipboard.
            // Therefore, assign the content its rightful type.
            else
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
        }
    }

    private void SetClipboardImage(IDataObject? dataObj)
    {
        // Determines whether an image has been cut/copied.
        if (_sharpClipboardInstance.ObservableFormats.Images &&
            dataObj?.GetDataPresent(DataFormats.Bitmap) != null)
        {
            Image? capturedImage = dataObj.GetData(DataFormats.Bitmap) as Image;
            _sharpClipboardInstance.ClipboardImage = capturedImage;

            _sharpClipboardInstance.Invoke(capturedImage, SharpClipboard.ContentTypes.Image,
                new SourceApplication(GetForegroundWindow(), SharpClipboard.ForegroundWindowHandle(),
                    GetApplicationName(), GetActiveWindowTitle(), GetApplicationPath()));
        }
    }


    private void SetClipboardText(IDataObject? dataObj)
    {
        // Determines whether text has been cut/copied.
        if (_sharpClipboardInstance.ObservableFormats.Texts &&
            dataObj != null &&
            (dataObj.GetDataPresent(DataFormats.Text) || dataObj.GetDataPresent(DataFormats.UnicodeText)))
        {
            string? capturedText = (dataObj.GetData(DataFormats.UnicodeText) ?? dataObj.GetData(DataFormats.Text))?.ToString();
            _sharpClipboardInstance.ClipboardText = capturedText;
            _sharpClipboardInstance.Invoke(capturedText, SharpClipboard.ContentTypes.Text,
                new SourceApplication(GetForegroundWindow(), SharpClipboard.ForegroundWindowHandle(),
                    GetApplicationName(), GetActiveWindowTitle(), GetApplicationPath()));
        }
    }

    private void SetClipboardOther(IDataObject? dataObj)
    {
        // Determines whether a complex object has been cut/copied.
        if (_sharpClipboardInstance.ObservableFormats.Others &&
            dataObj?.GetDataPresent(DataFormats.FileDrop) != null)
        {
            _sharpClipboardInstance.Invoke(dataObj, SharpClipboard.ContentTypes.Other,
                new SourceApplication(GetForegroundWindow(), SharpClipboard.ForegroundWindowHandle(),
                    GetApplicationName(), GetActiveWindowTitle(), GetApplicationPath()));
        }
    }


    /// <summary>
    /// This is the main clipboard detection method.
    /// Algorithmic customizations are most welcome.
    /// </summary>
    /// <param name="m">The processed window-reference message.</param>
    [SupportedOSPlatform("windows")]
    [HandlesResourceDisposal]
    [SuppressMessage("ReSharper", "CognitiveComplexity")]
    [SuppressMessage("ReSharper", "RedundantEmptySwitchSection")]
    [SuppressMessage("ReSharper", "UncatchableException")]
    [SuppressMessage("ReSharper", "MethodTooLong")]
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        switch (m)
        {
            case { Msg: WM_DRAWCLIPBOARD }:
                {
                    try
                    {
                        // If clipboard-monitoring is enabled, proceed to listening.
                        if (Ready && _sharpClipboardInstance.MonitorClipboard)
                        {
                            IDataObject? dataObj = GetClipboardDataObject();
                            SetClipboardFiles(dataObj);
                            SetClipboardImage(dataObj);
                            SetClipboardText(dataObj);
                            SetClipboardOther(dataObj);
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
                    finally
                    {
                        // Provides support for multi-instance clipboard monitoring.
                        _ = SendMessage(_chainedWnd, m.Msg, m.WParam, m.LParam);
                    }

                    break;
                }

            case { Msg: WM_CHANGECBCHAIN }:
                {
                    if (m.WParam == _chainedWnd)
                    {
                        _chainedWnd = m.LParam;
                    }
                    else
                    {
                        _ = SendMessage(_chainedWnd, m.Msg, m.WParam, m.LParam);
                    }

                    break;
                }

            default:
                {
                    break;
                }
        }
    }

    /// <summary>
    /// Start monitoring the clipboard.
    /// </summary>
    [PublicAPI]
    [SupportedOSPlatform("windows6.0")]
    public void StartMonitoring()
    {
        Show();
    }

    /// <summary>
    /// Stop monitoring the clipboard.
    /// </summary>
    [PublicAPI]
    [SupportedOSPlatform("windows6.0")]
    public void StopMonitoring()
    {
        Close();
    }

    [LibraryImport("user32.dll")]
    private static partial int GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [PublicAPI]
    private static partial IntPtr GetForegroundWindowPtr();

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetWindowText(IntPtr hWnd, [Out] char[] lpString, int nMaxCount);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(int hWnd, out int lpdwProcessId);

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
        catch (ArgumentException)
        {
        }

        return _executableName;
    }

    private string? GetApplicationPath()
    {
        return _executablePath;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    private static string? GetActiveWindowTitle()
    {
        const int capacity = 256;
        string? result = null;
        try
        {
            char[] buffer = new char[capacity];
            nint handle = SharpClipboard.ForegroundWindowHandle();
            int length = GetWindowText(handle, buffer, capacity);
            result = new string(buffer, 0, length);
        }
        catch (SystemException)
        {
        }


        return result;
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
        _ = ChangeClipboardChain(Handle, _chainedWnd);

        _chainedWnd = IntPtr.Zero;
    }
}
