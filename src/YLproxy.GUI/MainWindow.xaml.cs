using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using YLproxy.GUI.ViewModels;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace YLproxy.GUI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        InitializeTrayIconViewModel();
        InitializeKeyboardShortcuts();
    }

    private void InitializeTrayIconViewModel()
    {
        if (DataContext is MainViewModel vm)
        {
            vm.TrayIcon = new TrayIconViewModel(TrayIcon, vm);
        }

        DataContextChanged += (_, args) =>
        {
            if (args.NewValue is MainViewModel newVm)
            {
                newVm.TrayIcon = new TrayIconViewModel(TrayIcon, newVm);
            }
        };
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

    /// <summary>
    /// 最小化时隐藏到系统托盘
    /// </summary>
    private void OnStateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            if (DataContext is MainViewModel vm && vm.TrayIcon != null)
            {
                vm.TrayIcon.IsMinimized = true;
            }
        }
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
            TrayIcon?.Dispose();
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
        TrayIcon?.Dispose();
        base.OnClosed(e);
    }
}
