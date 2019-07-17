# KeyViz
A minimal visualizer of keyboard layouts for Windows.

Written using C# and WPF.
Currently requires JSON-parser from Newtonsoft to read JSON format keyboard specifications from QMK.

![Screenshot](screenshot.png "Screenshot")

So far hardcoded for use with TADA68. The JSON description file can be replaced with a custom mapping though.

See for example: [QMK Configurator](https://config.qmk.fm/#/tada68/LAYOUT_ansi)
## Usage
The application minimizes to the tray to take up minimal focus.
To show the first layer of the keyboard (usually the values printed on the keys) press Ctrl + 1.
To show any following layer press Ctrl + N, where N is larger than 1.