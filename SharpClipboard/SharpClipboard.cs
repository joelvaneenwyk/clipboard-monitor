﻿/*
 * Developer    : Willy Kimura (WK).
 * Library      : SharpClipboard.
 * License      : MIT.
 *
 * This handy library was designed to assist .NET developers
 * monitor the system clipboard in an easier and pluggable
 * fashion that before. It provides support for detecting
 * data formats including texts, images & files. To use it
 * at design-time, simply add the component in the Toolbox
 * then drag-n-drop it inside your Form to customize its
 * options and features. Improvements are always welcome.
 *
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows.Forms.Design.Behavior;
using JetBrains.Annotations;
using WK.Libraries.SharpClipboardNS.Views;
using Timer = System.Windows.Forms.Timer;

namespace WK.Libraries.SharpClipboardNS;

/// <summary>
/// Assists in anonymously monitoring the system clipboard by
/// detecting any copied/cut data and the type of data it is.
/// </summary>
[PublicAPI]
[Designer(typeof(WKDesigner))]
[DefaultEvent("ClipboardChanged")]
[DefaultProperty("MonitorClipboard")]
[Description("Assists in anonymously monitoring the system clipboard by " +
             "detecting any copied/cut data and the type of data it is.")]
public sealed partial class SharpClipboard : Component
{
    /// <summary>
    /// Provides a list of the supported clipboard content types.
    /// </summary>
    public enum ContentTypes
    {
        /// <summary>
        /// Represents <see cref="string" /> content.
        /// </summary>
        Text = 0,

        /// <summary>
        /// Represents <see cref="Image" /> content.
        /// </summary>
        Image = 1,

        /// <summary>
        /// Represents content as a <see cref="List{T}" /> of files.
        /// </summary>
        Files = 2,

        /// <summary>
        /// Represents any complex objects.
        /// </summary>
        Other = 3
    }

    private readonly Lazy<ClipboardHandle> _handle;

    [SupportedOSPlatform("windows6.0")]
    private readonly Timer _timer = new();

    private bool _monitorClipboard;

    private ObservableDataFormats _observableFormats = new();
    private bool _observeLastEntry;

    /// <summary>
    /// Initializes a new instance of <see cref="SharpClipboard" />.
    /// </summary>
    /// <param name="container">
    /// The container hosting the component.
    /// </param>
    [SupportedOSPlatform("windows6.0")]
    public SharpClipboard(IContainer? container = null)
    {
        _handle = new Lazy<ClipboardHandle>(
            () => new ClipboardHandle(this),
            LazyThreadSafetyMode.ExecutionAndPublication);

        container?.Add(this);

        InitializeComponent();

        SetDefaults();
    }

    /// <summary>
    /// Gets or sets a value indicating whether the clipboard
    /// will be monitored once the application launches.
    /// </summary>
    [Category("#Clipboard: Behaviour")]
    [Description("Sets a value indicating whether the clipboard " +
                 "will be monitored once the application launches.")]
    public bool MonitorClipboard
    {
        get => _monitorClipboard;
        set
        {
            _monitorClipboard = value;
            MonitorClipboardChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// When set to true, the last cut/copied clipboard item will
    /// not be auto-picked once monitoring is enabled. However when
    /// set to false, the last cut/copied clipboard item will be
    /// auto-picked once monitoring is enabled.
    /// </summary>
    [Category("#Clipboard: Behaviour")]
    [Description("When set to true, the last cut/copied clipboard item will " +
                 "be auto-picked once monitoring is enabled. However when " +
                 "set to false, the last cut/copied clipboard item will not " +
                 "be auto-picked once monitoring is enabled.")]
    public bool ObserveLastEntry
    {
        get => _observeLastEntry;
        set
        {
            _observeLastEntry = value;
            ObserveLastEntryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the data formats that will be observed
    /// or monitored when cut/copy actions are triggered.
    /// </summary>
    [Category("#Clipboard: Behaviour")]
    [Description("Sets the data formats that will be observed " +
                 "or monitored when cut/copy actions are triggered.")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ObservableDataFormats ObservableFormats
    {
        get => _observableFormats;
        set
        {
            _observableFormats = value;
            ObservableFormatsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets or sets the object that contains programmer-
    /// supplied data associated with the component.
    /// </summary>
    [Bindable(true)]
    [Category("#Clipboard: Miscellaneous")]
    [TypeConverter(typeof(StringConverter))]
    [Description("Sets the object that contains programmer-" +
                 "supplied data associated with the component.")]
    public object? Tag { get; set; }

    /// <summary>
    /// Gets the currently cut/copied clipboard text.
    /// </summary>
    [PublicAPI]
    [Browsable(false)]
    public string? ClipboardText { get; internal set; }

    /// <summary>
    /// Gets the currently cut/copied clipboard <see cref="object" />.
    /// This is necessary when handling complex content copied to the clipboard.
    /// </summary>
    [PublicAPI]
    [Browsable(false)]
    public object? ClipboardObject { get; internal set; }

    /// <summary>
    /// Gets the currently cut/copied clipboard file-path.
    /// </summary>
    [PublicAPI]
    [Browsable(false)]
    public string? ClipboardFile { get; internal set; }

    /// <summary>
    /// Gets the currently cut/copied clipboard file-paths.
    /// </summary>
    [PublicAPI]
    [Browsable(false)]
    public List<string?> ClipboardFiles { get; internal set; } = [];

    /// <summary>
    /// Gets the currently cut/copied clipboard image.
    /// </summary>
    [PublicAPI]
    [Browsable(false)]
    public Image? ClipboardImage { get; internal set; }

    /// <summary>
    /// Lets you change the invisible clipboard-window-handle's title
    /// that is designed to receive broadcasted clipboard messages. This is
    /// however unnecessary for normal users but may be essential if you're
    /// working under special circumstances that require supervision.
    /// The window will however remain hidden from all users.
    /// </summary>
    [PublicAPI]
    [Browsable(false)]
    public static string HandleCaption { get; set; } = string.Empty;

    /// <summary>
    /// Gets the current foreground window's handle.
    /// </summary>
    /// <returns></returns>
    public static IntPtr ForegroundWindowHandle()
    {
        return GetForegroundWindow();
    }

    /// <summary>
    /// Starts the clipboard-monitoring process and
    /// initializes the system clipboard-access handle.
    /// </summary>
    [PublicAPI]
    [SupportedOSPlatform("windows6.0")]
    public void StartMonitoring()
    {
        _handle.Value.Show();
    }

    /// <summary>
    /// Ends the clipboard-monitoring process and
    /// shuts the system clipboard-access handle.
    /// </summary>
    [PublicAPI]
    [SupportedOSPlatform("windows6.0")]
    public void StopMonitoring()
    {
        _handle.Value.Close();
    }

    /// <summary>
    /// Apply library-default settings and launch code.
    /// </summary>
    [SupportedOSPlatform("windows6.0")]
    private void SetDefaults()
    {
        _timer.Enabled = true;
        _timer.Interval = 1000;
        _timer.Tick += OnLoad;

        MonitorClipboard = true;
        ObserveLastEntry = true;
    }

    /// <summary>
    /// Invokes the <see cref="ClipboardChanged" /> event with formal parameters.
    /// </summary>
    /// <param name="content">The current clipboard content.</param>
    /// <param name="type">The current clipboard content-type.</param>
    /// <param name="source"></param>
    internal void Invoke(object? content, ContentTypes type, SourceApplication source)
    {
        ClipboardChanged?.Invoke(this, ClipboardChangedEventArgs.CreateInstance(content, type, source));
    }

    /// <summary>
    /// Gets the foreground or currently active window handle.
    /// </summary>
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// This event is triggered whenever the
    /// system clipboard has been modified.
    /// </summary>
    [Category("#Clipboard: Events")]
    [Description("This event is triggered whenever the " +
                 "system clipboard has been modified.")]
    public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;

    /// <summary>
    /// Occurs whenever the clipboard-monitoring status has been changed.
    /// </summary>
    [Category("#Clipboard: Events")]
    [Description("Occurs whenever the clipboard-monitoring status has been changed.")]
    public event EventHandler<EventArgs>? MonitorClipboardChanged;

    /// <summary>
    /// Occurs whenever the allowed observable formats have been changed.
    /// </summary>
    [Category("#Clipboard: Events")]
    [Description("Occurs whenever the allowed observable formats have been changed.")]
    public event EventHandler<EventArgs>? ObservableFormatsChanged;

    /// <summary>
    /// Occurs whenever the 'ObserveLastEntry' property has been changed.
    /// </summary>
    [Category("#Clipboard: Events")]
    [Description("Occurs whenever the allowed observable formats have been changed.")]
    public event EventHandler<EventArgs>? ObserveLastEntryChanged;

    /// <summary>
    /// This initiates a Timer that then begins the
    /// clipboard-monitoring service. The Timer will
    /// auto-shutdown once the service has started.
    /// </summary>
    [SupportedOSPlatform("windows6.0")]
    private void OnLoad(object? sender, EventArgs e)
    {
        if (!DesignMode)
        {
            _timer.Stop();
            _timer.Enabled = false;

            StartMonitoring();
        }
    }

    /// <summary>
    /// Provides data for the <see cref="ClipboardChanged" /> event.
    /// </summary>
    public class ClipboardChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Provides data for the <see cref="ClipboardChanged" /> event.
        /// </summary>
        /// <param name="content">The current clipboard content.</param>
        /// <param name="contentType">The current clipboard-content-type.</param>
        /// <param name="source"></param>
        private ClipboardChangedEventArgs(object? content, ContentTypes contentType, SourceApplication source)
        {
            Content = content;
            ContentType = contentType;
            SourceApplication = new SourceApplication(
                source.ID, source.Handle, source.Name, source.Title, source.Path);
        }

        /// <summary>
        /// Gets the currently copied clipboard content.
        /// </summary>
        [PublicAPI]
        public object? Content { get; }

        /// <summary>
        /// Gets the currently copied clipboard content-type.
        /// </summary>
        [PublicAPI]
        public ContentTypes ContentType { get; }

        /// <summary>
        /// Gets the application from where the
        /// clipboard's content were copied.
        /// </summary>
        [PublicAPI]
        public SourceApplication SourceApplication { get; }

        /// <summary>
        /// Creates a new instance of <see cref="ClipboardChangedEventArgs" />.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="contentType"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public static ClipboardChangedEventArgs CreateInstance(
            object? content, ContentTypes contentType, SourceApplication source)
        {
            return new ClipboardChangedEventArgs(content, contentType, source);
        }
    }

    /// <summary>
    /// Component designer for action lists.
    /// </summary>
    public class WKDesigner : ComponentDesigner
    {
        private DesignerActionListCollection? _actionLists;

        /// <summary>
        /// Use pull model to populate smart tag menu.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public override DesignerActionListCollection ActionLists
        {
            get
            {
                _actionLists ??= [new WKComponentActionList(Component)];
                return _actionLists;
            }
        }
    }

    /// <summary>
    /// Custom action list for clipboard component.
    /// </summary>
    public class WKComponentActionList : DesignerActionList
    {
        private readonly DesignerActionUIService? designerActionUISvc;
        private readonly SharpClipboard? WKComponent;

        /// <summary>
        /// Initializes a new instance of the <see cref="WKComponentActionList" /> class.
        /// </summary>
        /// <param name="component"></param>
        [SupportedOSPlatform("windows")]
        public WKComponentActionList(IComponent component) : base(component)
        {
            WKComponent = component as SharpClipboard;

            // Cache a reference to DesignerActionUIService so
            // that the DesignerActionList can be refreshed.
            designerActionUISvc = GetService(typeof(DesignerActionUIService))
                as DesignerActionUIService;

            // Automatically display Smart Tags for quick access
            // to the most common properties needed by users.
            AutoShow = true;
        }

        /// <summary>
        /// Property tracking whether or not the clipboard is monitored.
        /// </summary>
        [PublicAPI]
        public bool MonitorClipboard
        {
            get => WKComponent?.MonitorClipboard ?? false;
            set => SetValue(WKComponent, nameof(MonitorClipboard), value);
        }

        private static PropertyDescriptor? GetPropertyDescriptor(IComponent? component, string propertyName)
        {
            return component == null ? null : TypeDescriptor.GetProperties(component)[propertyName];
        }

        private static IDesignerHost? GetDesignerHost(IComponent? component)
        {
            return component?.Site?.GetService(typeof(IDesignerHost)) as IDesignerHost;
        }

        private static IComponentChangeService? GetChangeService(IComponent? component)
        {
            return component?.Site?.GetService(typeof(IComponentChangeService)) as IComponentChangeService;
        }

        private static void SetValue(IComponent? component, string propertyName, object value)
        {
            PropertyDescriptor? propertyDescriptor = GetPropertyDescriptor(component, propertyName);
            IComponentChangeService? svc = GetChangeService(component);
            IDesignerHost? host = GetDesignerHost(component);
            DesignerTransaction? txn = host?.CreateTransaction();

            try
            {
                if (component != null)
                {
                    svc?.OnComponentChanging(component, propertyDescriptor);
                    propertyDescriptor?.SetValue(component, value);
                    svc?.OnComponentChanged(component, propertyDescriptor, null, null);
                }
                txn?.Commit();
                txn = null;
            }
            finally
            {
                txn?.Cancel();
            }
        }

        /// <summary>
        /// Implementation of this abstract method creates Smart Tag items,
        /// associates their targets, and collects them into a list.
        /// </summary>
        [SupportedOSPlatform("windows")]
        public override DesignerActionItemCollection GetSortedActionItems()
        {
            DesignerActionItemCollection items =
            [
                new DesignerActionHeaderItem(nameof(Behavior)),
                new DesignerActionPropertyItem(nameof(MonitorClipboard),
                    "Monitor Clipboard", "Behaviour",
                    GetPropertyDescriptor(Component, nameof(MonitorClipboard))?.Description ?? string.Empty)
            ];

            return items;
        }
    }
}

/// <summary>
/// Provides a list of supported observable data formats
/// that can be monitored from the system clipboard.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[TypeConverter(typeof(ExpandableObjectConverter))]
[Description("Provides a list of supported observable data formats " +
             "that can be monitored from the system clipboard.")]
public class ObservableDataFormats
{
    private bool _all;

    /// <summary>
    /// Creates a new <see cref="ObservableDataFormats" /> options class-instance.
    /// </summary>
    public ObservableDataFormats()
    {
        _all = true;
    }

    /// <summary>
    /// Gets or sets a value indicating whether all the
    /// supported observable formats will be monitored.
    /// </summary>
    [ParenthesizePropertyName(true)]
    [Category("#Clipboard: Behaviour")]
    [Description("Sets a value indicating whether all the supported " +
                 "observable formats will be monitored.")]
    public bool All
    {
        get => _all;
        set
        {
            _all = value;

            Texts = value;
            Files = value;
            Images = value;
            Others = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether texts will be monitored.
    /// </summary>
    [Category("#Clipboard: Behaviour")]
    [Description("Sets a value indicating whether texts will be monitored.")]
    public bool Texts { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether files will be monitored.
    /// </summary>
    [Category("#Clipboard: Behaviour")]
    [Description("Sets a value indicating whether files will be monitored.")]
    public bool Files { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether images will be monitored.
    /// </summary>
    [Category("#Clipboard: Behaviour")]
    [Description("Sets a value indicating whether images will be monitored.")]
    public bool Images { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether other
    /// complex object-types will be monitored.
    /// </summary>
    [Category("#Clipboard: Behaviour")]
    [Description("Sets a value indicating whether other " +
                 "complex object-types will be monitored.")]
    public bool Others { get; set; } = true;

    /// <summary>
    /// Returns a <see cref="string" /> containing the list of observable data
    /// formats provided and their applied statuses separated by semi-colons.
    /// </summary>
    public override string ToString()
    {
        return $"Texts: {Texts}; Images: {Images}; Files: {Files}; Others: {Others}";
    }
}

/// <summary>
/// Stores details of the application from
/// where the clipboard's content were copied.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class SourceApplication
{
    /// <summary>
    /// Creates a new <see cref="SourceApplication" /> class-instance.
    /// </summary>
    /// <param name="id">The application's ID.</param>
    /// <param name="handle">The application's handle.</param>
    /// <param name="name">The application's name.</param>
    /// <param name="title">The application's title.</param>
    /// <param name="path">The application's path.</param>
    internal SourceApplication(
        int id, IntPtr handle,
        string? name, string? title, string? path)
    {
        ID = id;
        Name = name;
        Path = path;
        Title = title;
        Handle = handle;
    }

    /// <summary>
    /// Gets the application's process-ID.
    /// </summary>
    public readonly int ID;

    /// <summary>
    /// Gets the application's window-handle.
    /// </summary>
    public readonly IntPtr Handle;

    /// <summary>
    /// Gets the application's name.
    /// </summary>
    public readonly string? Name;

    /// <summary>
    /// Gets the application's title-text.
    /// </summary>
    public readonly string? Title;

    /// <summary>
    /// Gets the application's absolute path.
    /// </summary>
    public readonly string? Path;

    /// <summary>
    /// Returns a <see cref="string" /> containing the list
    /// of application details provided.
    /// </summary>
    public override string ToString()
    {
        return $"ID: {ID}; Handle: {Handle}, Name: {Name}; " +
               $"Title: {Title}; Path: {Path}";
    }
}
