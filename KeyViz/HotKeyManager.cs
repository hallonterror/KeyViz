using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows;

namespace KeyViz
{
  public static class Constants
  {
    //modifiers
    public const int NOMOD = 0x0000;
    public const int ALT = 0x0001;
    public const int CTRL = 0x0002;
    public const int SHIFT = 0x0004;
    public const int WIN = 0x0008;

    //windows message id for hotkey
    public const int WM_HOTKEY_MSG_ID = 0x0312;
  }

  class HotKeyHandler
  {
    private int modifier;
    private int key;
    private IntPtr hWnd;
    private int id;

    public HotKeyHandler(int modifier, Keys key, Window window)
    {
      WindowInteropHelper helper = new WindowInteropHelper(window);
      this.modifier = modifier;
      this.key = (int)key;
      this.hWnd = helper.Handle;
      id = GetHashCode();
    }

    ~HotKeyHandler()
    {
      Unregister();
    }

    public bool Register()
    {
      return RegisterHotKey(hWnd, id, modifier, key);
    }

    public bool Unregister()
    {
      return UnregisterHotKey(hWnd, id);
    }

    public override int GetHashCode()
    {
      return modifier ^ key ^ hWnd.ToInt32();
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
  }

  class HotKeyManager
  {
    private Dictionary<int, HotKeyHandler> hotKeys = new Dictionary<int, HotKeyHandler>();

    public int Add(int modifier, Keys key, Window window)
    {
      HotKeyHandler hdl = new HotKeyHandler(modifier, key, window);
      int id = hdl.GetHashCode();
      if (hotKeys.ContainsKey(id))
        return -1;

      hdl.Register();
      hotKeys[id] = hdl;
      return id;
    }

    public void Clear()
    {
      hotKeys.Clear();
    }

    public int MatchHook(int msg, IntPtr wParam)
    {
      if (msg == Constants.WM_HOTKEY_MSG_ID)
      {
        foreach (var hk in hotKeys)
        {
          Int32 id = wParam.ToInt32();
          if (id == hk.Key)
          {
            return id;
          }
        }
      }
      return -1;
    }
  };
}
