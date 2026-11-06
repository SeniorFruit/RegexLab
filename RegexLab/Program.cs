using System;
using System.Windows;

namespace RegexLab;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new Application();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("Themes/Styles.xaml", UriKind.Relative)
        });

        app.Run(new MainWindow());
    }
}
