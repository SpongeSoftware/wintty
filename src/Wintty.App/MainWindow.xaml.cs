using Microsoft.UI.Xaml;
using Wintty.Core;

namespace Wintty.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = $"wintty {VersionInfo.Current}";
        Terminal.TitleChanged += (_, title) => Title = title;
        Terminal.ProcessExited += (_, _) => Close();
    }
}
