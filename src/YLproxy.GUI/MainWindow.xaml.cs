using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Input;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace YLproxy.GUI;

public partial class MainWindow : Window
{
    private System.Windows.Forms.NotifyIcon? _notifyIcon;

    public MainWindow()
    {
        InitializeComponent();
        InitializeTrayIcon();
        InitializeKeyboardShortcuts();
    }



    private void InitializeKeyboardShortcuts()
    {
        KeyDown += (_, e) =>
        {
            if (DataContext is not MainViewModel vm) return;

            switch (e.Key)
            {
                case Key.T when Keyboard.Modifiers == ModifierKeys.Control:
                    if (vm.TestCommand.CanExecute(null))
                        vm.TestCommand.Execute(null);
                    break;
                case Key.S when Keyboard.Modifiers == ModifierKeys.Control:
                    if (vm.StartCommand.CanExecute(null))
                        vm.StartCommand.Execute(null);
                    break;
                case Key.W when Keyboard.Modifiers == ModifierKeys.Control:
                    if (vm.StopCommand.CanExecute(null))
                        vm.StopCommand.Execute(null);
                    break;
                case Key.F when Keyboard.Modifiers == ModifierKeys.Control:
                    // Focus search box — handled in code-behind via SearchBox
                    break;
                case Key.Delete:
                    if (vm.RemoveCommand.CanExecute(null))
                        vm.RemoveCommand.Execute(null);
                    break;
            }
        };
    }

    private void InitializeTrayIcon()
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "YLproxy - 本地代理管理器"
        };

        _notifyIcon.DoubleClick += (_, _) => RestoreFromTray();

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add("显示窗口", null, (_, _) => RestoreFromTray());
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        contextMenu.Items.Add("退出", null, (_, _) => ExitApplication());
        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private async void ExitApplication()
    {
        var result = MessageBox.Show(
            "确定要退出 YLproxy 吗？\n\n所有正在运行的代理将被停止。",
            "YLproxy - 退出确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            if (DataContext is MainViewModel vm)
                await vm.ShutdownAsync();
            _notifyIcon?.Dispose();
            Application.Current.Shutdown();
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        // Minimize to tray instead of closing
        e.Cancel = true;
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        _notifyIcon?.Dispose();
        base.OnClosed(e);
    }
}
