using System;
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
using System.Text.RegularExpressions;
using System.Net;

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
    private System.Windows.Forms.NotifyIcon trayIcon = new System.Windows.Forms.NotifyIcon();
    private HotKeyManager hotKeyManager = new HotKeyManager();
    private Dictionary<int, Int32> hotKeyEventToKeyboardLayer = new Dictionary<int, Int32>();
    private List<List<List<String>>> keyboardLayer = new List<List<List<String>>>();

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
    public String KeyboardName { get; set; }
    private Int32 selectedLayer = -1;
    public Int32 SelectedLayer
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

        // Display the new active layer
        ShowLayer(selectedLayer);
      }
    }
    public Int32 AvailableLayers
    {
      get { return keyboardLayer.Count; }
    }
    private bool IsMinimized { get; set; }


    // ========================================================================
    // Getting key aliases and symbols
    // ========================================================================
    private Dictionary<string, string> keyToSymbol = new Dictionary<string, string>();
    // This function parses an official markdown file from QMK to display
    // nicer labels than the raw key codes found in the JSON/C-files.
    private void GetKeySymbols()
    {
      Regex rx = new Regex(@"\|`([^\|]*)`\s*\|([^\|]*)\|([^\|]*)\||\|`([^\|]*)`\s*\|([^\|]*)\|",
          RegexOptions.Compiled | RegexOptions.IgnoreCase);

      Regex andsplit = new Regex(@"`(.*)` and `(.*)`");

      System.IO.StreamReader file = new System.IO.StreamReader(GetPath("qmk_keys.md"));
      string line;
      while ((line = file.ReadLine()) != null)
      {
        MatchCollection matches = rx.Matches(line);
        foreach (Match match in matches)
        {
          GroupCollection groups = match.Groups;

          // Alias or no alias
          string symbol;
          List<string> keys = new List<string>();
          if (groups[4].Value != "")
          {
            keys.Add(groups[4].Value);
            symbol = groups[5].Value.Trim();
          }
          else
          {
            keys.Add(groups[1].Value);
            string aliasTemp = groups[2].Value.Trim();
            if (aliasTemp != "")
            {
              foreach (var alias in aliasTemp.Split(','))
                keys.Add(alias.Replace("`", "").Trim());
            }
            symbol = groups[3].Value.Trim();
          }

          // Skip no-op keycodes and their aliases
          if (keys[0] == "KC_NO" || keys[0] == "KC_TRANSPARENT")
          {
            continue;
          }

          // Figure out if we are working on a character code (eg. KC_A, KC_B, ...).
          // These keycode looks best if they are simply converted by removing KC_.
          if (keys[0].Substring(0, 3) == "KC_" && keys[0].Length == 4)
          {
            continue;
          }
          
          // Remove nasty HTML stuff
          symbol = WebUtility.HtmlDecode(symbol);
          symbol = symbol.Replace("<code>", "`").Replace("</code>", "`");

          // Skip string that are way too long to be displayed
          if (symbol.Length > 45)
          {
            continue;
          }

          // Shorten expression containing and
          MatchCollection splits = andsplit.Matches(symbol);
          if (splits.Count > 0)
          {
            symbol = splits[0].Groups[1].Value + " " + splits[0].Groups[2].Value;
          }

          // Remove all vowels to shorten text
          //symbol = symbol[0] + new string(symbol.Substring(1).Where(c => !"AEIOUYaeiouy".Contains(c)).ToArray());

          // Finally add the symbol as text for all keycodes
          foreach (var key in keys)
          {
            keyToSymbol[key] = symbol;
            Console.WriteLine("'{0}' key with symbol '{1}'", key, symbol);
          }
        }
      }
    }

    // ========================================================================
    // Building the main view
    // ========================================================================
    // Function called on the raw keycode
    // Modify this to get a prettier label on the keys
    private String CleanKeyName(String keyname)
    {
      if (keyToSymbol.ContainsKey(keyname)) { return keyToSymbol[keyname]; }
      if (keyname.Equals("KC_TRNS")) { return ""; }
      String cleanedKeyName = keyname.Replace("KC_", "");
      return cleanedKeyName;
    }
    // Function that builds the actual graphical element displayed
    // in the application. Modify this to get a nicer button look.
    private Button CreateGraphicalKey(Int32 row, Int32 column, String key, float KeySize, float[][] keySizes)
    {
      Button block = new Button();
      TextBlock content = new TextBlock();
      content.TextWrapping = TextWrapping.Wrap;
      content.Text = CleanKeyName(key);
      block.Content = content;
      block.Width = KeySize * keySizes[row][column];
      block.Height = KeySize;
      block.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
      block.VerticalAlignment = System.Windows.VerticalAlignment.Center;
      if (content.Text != "")
        block.ToolTip = content.Text;
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
        KeyboardName = data.keyboard;
        Int32[] keysPerRow = GetKeysPerRow(KeyboardName);

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
          keyboardLayer.Add(keys);
          keys = new List<List<String>>();
        }
      }
      catch
      {
        // Fail silently for now
        return;
      }

      // Add buttons to toggle between layers
      for (Int32 l = 0; l < keyboardLayer.Count; ++l)
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
      if (layerIndex >= keyboardLayer.Count)
      {
        // A non-existent layer was requested.
        // This should normally not happen as no buttons
        // or events should be created with a higher index.
        return;
      }

      // Generate a graphical visualization for the main window
      float[][] keySizes = GetKeySizes(KeyboardName, keyboardLayer[0]);
      float baseSize = 50.0f;
      LayoutPanel.Children.Clear();

      List<List<String>> layer = keyboardLayer[layerIndex];
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

      BoardName.Text = KeyboardName;
    }

    // ========================================================================
    // Events
    // ========================================================================
    // Handle a button press in the main window to select layer
    private void SelectLayerButton_Click(object sender, RoutedEventArgs e)
    {
      IndexedButton b = (IndexedButton)sender;
      SelectedLayer = b.Index;
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
      GetKeySymbols();
      PopulateKeymap();

      // Display the default layer in GUI
      SelectedLayer = 0;
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
        int id = hotKeyManager.Add(Constants.CTRL, System.Windows.Forms.Keys.D1 + i, this);
        hotKeyEventToKeyboardLayer[id] = i;
      }
    }

    // We should clean up our hotkeys to avoid having orphaned events in Windows
    protected override void OnClosed(EventArgs e)
    {
      _source.RemoveHook(HwndHook);
      _source = null;
      // Ensure that all events are deregistered properly
      hotKeyManager.Clear();
      base.OnClosed(e);
    }

    // This event is called once a global hotkey is pressed
    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
      int id = hotKeyManager.MatchHook(msg, wParam);
      if (id > 0)
      {
        Int32 layer = hotKeyEventToKeyboardLayer[id];
        SelectedLayer = layer;
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
