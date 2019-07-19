﻿using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using Newtonsoft.Json;

namespace KeyViz
{
  // Specialized button that can remember its index
  class IndexedButton : System.Windows.Controls.Primitives.ToggleButton
  {
    public Int32 Index
    {
      get;
      set;
    }
  };

  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    // ========================================================================
    // State variables
    // ========================================================================
    private HotKeyManager hkManager = new HotKeyManager();
    private Dictionary<int, Int32> hkHash2Layer = new Dictionary<int, Int32>();
    private System.Windows.Forms.NotifyIcon trayIcon = new System.Windows.Forms.NotifyIcon();
    private String keyboardName = "";
    private List<List<List<String>>> layers = new List<List<List<String>>>();

    // ========================================================================
    // Utilities
    // ========================================================================
    // Navigates to the app resource directory
    public String GetPath(String file)
    {
      return System.IO.Path.Combine(Environment.CurrentDirectory, @"resources\", file);
    }

    // These are keyboard specific
    public Int32[] GetKeysPerRow(String keyboard)
    {
      if (keyboard.Equals("tada68"))
      {
        return new Int32[] { 15, 15, 14, 14, 10 };
      }
      return new Int32[] { };
    }
    public float[][] GetKeySizes(String keyboard, List<List<String>> toplayer)
    {
      if (keyboard.Equals("tada68"))
      {
        Dictionary<String, float> dict = new Dictionary<String, float>();
        dict.Add("KC_SPC", 6.25f);
        dict.Add("KC_ENT", 2.25f);
        dict.Add("KC_LSFT", 2.25f);
        dict.Add("KC_BSPC", 2.0f);
        dict.Add("KC_CAPS", 1.75f);
        dict.Add("KC_RSFT", 1.75f);
        dict.Add("KC_TAB", 1.5f);
        dict.Add("KC_BSLS", 1.5f);

        dict.Add("KC_LCTL", 1.25f);
        dict.Add("KC_LGUI", 1.25f);
        dict.Add("KC_LALT", 1.25f);

        float[][] total = new float[toplayer.Count][];
        for (Int32 r = 0; r < toplayer.Count; ++r)
        {
          List<String> row = toplayer[r];
          total[r] = new float[row.Count];
          for (Int32 i = 0; i < row.Count; ++i)
          {
            String key = row[i];
            total[r][i] = dict.ContainsKey(key) ? dict[key] : 1.0f;
          }
        }
        return total;
      }
      return new float[][] { };
    }

    // ========================================================================
    // Properties
    // ========================================================================
    private Int32 selectedLayer = 0;
    private Int32 SelectedLayer
    {
      get
      {
        return selectedLayer;
      }
      set
      {
        if (selectedLayer == value)
        {
          return;
        }

        selectedLayer = value;

        // Update button look and feel
        foreach (IndexedButton b in LayoutList.Children)
        {
          b.IsChecked = selectedLayer == b.Index;
        }
      }
    }
    private Int32 AvailableLayers
    {
      get { return layers.Count; }
    }
    private bool IsMinimized { get; set; }

    // ========================================================================
    // Building the main view
    // ========================================================================
    // Function called on the raw keycode
    // Modify this to get a prettier label on the keys
    private String CleanKeyName(String keyname)
    {
      if (keyname.Equals("KC_TRNS")) { return ""; }
      String cleanedKeyName = keyname.Replace("KC_", "");
      return cleanedKeyName;
    }
    // Function that builds the actual graphical element displayed
    // in the application. Modify this to get a nicer button look.
    private Button CreateGraphicalKey(Int32 row, Int32 column, String key, float KeySize, float[][] keySizes)
    {
      Button block = new Button();
      block.Content = CleanKeyName(key);
      block.Width = KeySize * keySizes[row][column];
      block.Height = KeySize;
      block.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
      block.VerticalAlignment = System.Windows.VerticalAlignment.Center;
      return block;
    }

    // This function parses the keyboard layout specification
    // and creates the keyboard visualization using the above
    // declared helper functions.
    private void PopulateKeymap()
    {
      // Parse the source JSON
      try
      {
        String json = System.IO.File.ReadAllText(GetPath("keymap_default_tada68.json"));
        dynamic data = JsonConvert.DeserializeObject(json);
        keyboardName = data.keyboard;
        Int32[] keysPerRow = GetKeysPerRow(keyboardName);

        // Split into easy to understand layers
        var dlayers = data.layers;
        foreach (var dlayer in dlayers)
        {
          // Split the layers into rows
          List<List<String>> keys = new List<List<String>>();

          List<String> row = new List<string>();
          for (Int32 key = 0; key < dlayer.Count; ++key)
          {
            row.Add(dlayer[key].Value);
            if (row.Count >= keysPerRow[keys.Count])
            {
              keys.Add(row);
              row = new List<string>();
            }
          }

          if (keys.Count != keysPerRow.Length)
          {
            // TODO: Log warning here
          }
          layers.Add(keys);
          keys = new List<List<String>>();
        }
      }
      catch
      {
        // Fail silently for now
        return;
      }

      // Add buttons to toggle between layers
      for (Int32 l = 0; l < layers.Count; ++l)
      {
        IndexedButton b = new IndexedButton();
        b.Content = l.ToString();
        b.Index = l;
        b.Click += SelectLayerButton_Click;
        b.Width = 35;
        LayoutList.Children.Add(b);
      }
    }

    // This function displayes a certain layer for the loaded keyboard keymap.
    private void ShowLayer(Int32 layerIndex)
    {
      if (layerIndex >= layers.Count)
      {
        // A non-existent layer was requested.
        // This should normally not happen as no buttons
        // or events should be created with a higher index.
        return;
      }

      // Generate a graphical visualization for the main window
      float[][] keySizes = GetKeySizes(keyboardName, layers[0]);
      float baseSize = 50.0f;
      LayoutPanel.Children.Clear();

      List<List<String>> layer = layers[layerIndex];
      for (Int32 r = 0; r < layer.Count; ++r)
      {
        List<String> row = layer[r];
        StackPanel gridRow = new StackPanel();
        for (Int32 k = 0; k < row.Count; ++k)
        {
          gridRow.Children.Add(CreateGraphicalKey(r, k, row[k], baseSize, keySizes));
        }
        gridRow.Orientation = System.Windows.Controls.Orientation.Horizontal;
        LayoutPanel.Children.Add(gridRow);
      }

      BoardName.Text = keyboardName;
      SelectedLayer = layerIndex;
    }

    // ========================================================================
    // Events
    // ========================================================================
    // Handle a button press in the main window to select layer
    private void SelectLayerButton_Click(object sender, RoutedEventArgs e)
    {
      IndexedButton b = (IndexedButton)sender;
      ShowLayer(b.Index);
    }
    // Handle click on tray icon to restore main window or
    // show the menu with available options.
    private void TrayIcon_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
    {
      if (e.Button.Equals(System.Windows.Forms.MouseButtons.Left))
      {
        if (IsMinimized)
        {
          RestoreFromTray();
          IsMinimized = false;
        }
      }
      else if (e.Button.Equals(System.Windows.Forms.MouseButtons.Right))
      {
        // TODO: Spawn the menu here..
        //trayIcon.ContextMenuStrip.Show();
      }
    }

    // ========================================================================
    // Main setup
    // ========================================================================
    // Helper that encapsulates the setup of the tray icon.
    private void CreateTaskBarNotifyIcon()
    {
      //Icon: probably the most important property that represents the icon that will be shown in the system tray.Only.ico files can be used.
      trayIcon.Icon = new System.Drawing.Icon(GetPath("icon_16.ico"));

      trayIcon.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
      trayIcon.BalloonTipText = "The keymap visualization tool is running in the background.";
      trayIcon.BalloonTipTitle = "Minimize to tray";
      trayIcon.Text = "Press Ctrl + 1 to show the first layer of your current keymap.";

      // TODO: Fix a nice looking menu
      ////ContextMenuStrip: context menu that is associated with the NotifyIcon.
      //System.Windows.Forms.ContextMenuStrip menu = new ContextMenuStrip();
      //trayIcon.ContextMenuStrip = menu;

      // Bring back GUI
      trayIcon.MouseClick += TrayIcon_MouseClick;
    }

    // Constructor of the main window.
    public MainWindow()
    {
      InitializeComponent();
      CreateTaskBarNotifyIcon();
      PopulateKeymap();

      // Display the default layer in GUI
      ShowLayer(0);
    }

    // ========================================================================
    // Handle minimize/restore of main window
    // ========================================================================
    private bool notifyOnce = true;
    private void MinimizeToTray()
    {
      Hide();
      WindowState = WindowState.Minimized;
      trayIcon.Visible = true;

      if (notifyOnce)
      {
        trayIcon.ShowBalloonTip(1000);
        notifyOnce = false;
      }
    }
    private void RestoreFromTray()
    {
      Show();
      WindowState = WindowState.Normal;
      trayIcon.Visible = false;
    }
    private void StartHideTimer()
    {
      var timer = new System.Windows.Threading.DispatcherTimer();
      timer.Interval = TimeSpan.FromSeconds(0.2);
      timer.Tick += ((sender, e) =>
      {
        if (!Keyboard.IsKeyDown(Key.LeftCtrl))
        {
          MinimizeToTray();
          timer.Stop();
        }
      });
      timer.Start();
    }

    // Minimize to system tray when application is minimized.
    protected override void OnStateChanged(EventArgs e)
    {
      if (WindowState == WindowState.Minimized)
      {
        if (!IsMinimized)
        {
          MinimizeToTray();
          IsMinimized = true;
        }
      }
      base.OnStateChanged(e);
    }
    // ========================================================================
    // The following code handles global hotkey support
    // ========================================================================
    private System.Windows.Interop.HwndSource _source;

    // Hotkey events must be assigned here, otherwise they don't work
    protected override void OnSourceInitialized(EventArgs e)
    {
      base.OnSourceInitialized(e);

      // Get hardware handle to use for hotkeys
      var helper = new System.Windows.Interop.WindowInteropHelper(this);
      _source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
      _source.AddHook(HwndHook);

      // Register our selected hotkeys
      for (Int32 i = 0; i < AvailableLayers; ++i)
      {
        int id = hkManager.Add(Constants.CTRL, System.Windows.Forms.Keys.D1 + i, this);
        hkHash2Layer[id] = i;
      }
    }

    // We should clean up our hotkeys to avoid having orphaned events in Windows
    protected override void OnClosed(EventArgs e)
    {
      _source.RemoveHook(HwndHook);
      _source = null;
      // Ensure that all events are deregistered properly
      hkManager.Clear();
      base.OnClosed(e);
    }

    // This event is called once a global hotkey is pressed
    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
      int id = hkManager.MatchHook(msg, wParam);
      if (id > 0)
      {
        Int32 layer = hkHash2Layer[id];
        ShowLayer(layer);
        RestoreFromTray();
        handled = true;

        if (IsMinimized)
        {
          StartHideTimer();
        }
      }
      return IntPtr.Zero;
    }
    // ========================================================================
  }
}
