using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GatTelemetry;

internal sealed partial class MainForm
{
    private string RoadLifeThemePrepareXaml(string xaml)
    {
        if (string.IsNullOrEmpty(xaml)) return xaml;
        xaml = xaml.Replace("Value=\"{StaticResource SoftEdgeBrush}\"", "Value=\"{DynamicResource SoftEdgeBrush}\"");
        xaml = xaml.Replace("Stroke=\"{StaticResource EdgeBrush}\"", "Stroke=\"{DynamicResource EdgeBrush}\"");
        xaml = xaml.Replace("BorderBrush=\"#5536F27A\"", "BorderBrush=\"{DynamicResource SeparatorBrush}\"");
        xaml = xaml.Replace(
            "<SolidColorBrush x:Key=\"SoftEdgeBrush\" Color=\"#B836F27A\"/>",
            "<SolidColorBrush x:Key=\"SoftEdgeBrush\" Color=\"#B836F27A\"/>\r\n    <SolidColorBrush x:Key=\"SeparatorBrush\" Color=\"#5536F27A\"/>");

        string dots =
            "      <Ellipse Width=\"6\" Height=\"6\" Fill=\"{StaticResource EdgeBrush}\" Margin=\"2,0\"/>\r\n" +
            "      <Ellipse Width=\"6\" Height=\"6\" Fill=\"{StaticResource EdgeBrush}\" Margin=\"2,0\"/>\r\n" +
            "      <Ellipse Width=\"6\" Height=\"6\" Fill=\"{StaticResource EdgeBrush}\" Margin=\"2,0\"/>\r\n" +
            "      <Ellipse Width=\"6\" Height=\"6\" Fill=\"{StaticResource EdgeBrush}\" Margin=\"2,0\"/>";
        string buttons =
            "      <Ellipse Name=\"ThemeGreen\" Width=\"10\" Height=\"10\" Fill=\"#FF36F27A\" Stroke=\"White\" StrokeThickness=\"0.5\" Margin=\"3,0\" Cursor=\"Hand\" ToolTip=\"Verde\"/>\r\n" +
            "      <Ellipse Name=\"ThemeBlue\" Width=\"10\" Height=\"10\" Fill=\"#FF2196F3\" Stroke=\"White\" StrokeThickness=\"0.5\" Margin=\"3,0\" Cursor=\"Hand\" ToolTip=\"Azul\"/>\r\n" +
            "      <Ellipse Name=\"ThemeYellow\" Width=\"10\" Height=\"10\" Fill=\"#FFFFD43B\" Stroke=\"White\" StrokeThickness=\"0.5\" Margin=\"3,0\" Cursor=\"Hand\" ToolTip=\"Amarelo\"/>\r\n" +
            "      <Ellipse Name=\"ThemeRed\" Width=\"10\" Height=\"10\" Fill=\"#FFFF4545\" Stroke=\"White\" StrokeThickness=\"0.5\" Margin=\"3,0\" Cursor=\"Hand\" ToolTip=\"Vermelho\"/>";
        return xaml.Replace(dots, buttons);
    }

    private void RoadLifeThemeWire()
    {
        WireRoadLifeTheme("ThemeGreen", "#FF36F27A");
        WireRoadLifeTheme("ThemeBlue", "#FF2196F3");
        WireRoadLifeTheme("ThemeYellow", "#FFFFD43B");
        WireRoadLifeTheme("ThemeRed", "#FFFF4545");
    }

    private void WireRoadLifeTheme(string name, string color)
    {
        if (_roadLifeOverlay == null) return;
        Ellipse dot = _roadLifeOverlay.FindName(name) as Ellipse;
        if (dot == null) return;
        dot.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            SetRoadLifeTheme(color);
        };
    }

    private void SetRoadLifeTheme(string edgeHex)
    {
        if (_roadLifeOverlay == null) return;
        Color c = (Color)ColorConverter.ConvertFromString(edgeHex);
        _roadLifeOverlay.Resources["EdgeBrush"] = new SolidColorBrush(c);
        _roadLifeOverlay.Resources["SoftEdgeBrush"] = new SolidColorBrush(Color.FromArgb(0xB8, c.R, c.G, c.B));
        _roadLifeOverlay.Resources["SeparatorBrush"] = new SolidColorBrush(Color.FromArgb(0x55, c.R, c.G, c.B));
    }
}
