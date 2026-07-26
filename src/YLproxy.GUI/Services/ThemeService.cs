using System;
using System.Windows;
using System.Windows.Media;
using Application = System.Windows.Application;

namespace YLproxy.GUI.Services;

/// <summary>
/// 主题管理服务，支持暗色/浅色主题切换
/// </summary>
public sealed class ThemeService
{
    private static ThemeService? _instance;
    public static ThemeService Instance => _instance ??= new ThemeService();

    private ResourceDictionary? _currentTheme;
    private string _currentThemeName = string.Empty;

    /// <summary>
    /// 当前主题名称
    /// </summary>
    public string CurrentThemeName => _currentThemeName;

    /// <summary>
    /// 主题变更事件
    /// </summary>
    public event EventHandler<string>? ThemeChanged;

    /// <summary>
    /// 应用指定主题
    /// </summary>
    public void ApplyTheme(string themeName)
    {
        var app = Application.Current;
        if (app == null) return;

        // 移除当前主题
        if (_currentTheme != null)
        {
            app.Resources.MergedDictionaries.Remove(_currentTheme);
        }

        // 加载新主题
        var themeUri = new Uri($"/Themes/{themeName}.xaml", UriKind.Relative);
        _currentTheme = new ResourceDictionary { Source = themeUri };
        app.Resources.MergedDictionaries.Add(_currentTheme);

        _currentThemeName = themeName;
        ThemeChanged?.Invoke(this, themeName);
    }

    /// <summary>
    /// 根据系统主题自动切换
    /// </summary>
    public void ApplySystemTheme()
    {
        try
        {
            var color = SystemParameters.WindowGlassColor;
            var brightness = (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) / 255.0;
            ApplyTheme(brightness < 0.5 ? "DarkTheme" : "LightTheme");
        }
        catch
        {
            ApplyTheme("DarkTheme");
        }
    }

    /// <summary>
    /// 切换当前主题
    /// </summary>
    public void ToggleTheme()
    {
        if (_currentThemeName == "DarkTheme")
            ApplyTheme("LightTheme");
        else
            ApplyTheme("DarkTheme");
    }
}
