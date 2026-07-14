using System.Windows;
using YLproxy.GUI.ViewModels;

namespace YLproxy.GUI.Views;

public partial class AddProxyWindow : Window
{
    public AddProxyWindow()
    {
        InitializeComponent();
    }

    private void OnCancelClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        Close();
    }

    private void OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not AddProxyViewModel vm) return;
        if (sender is System.Windows.Controls.PasswordBox pb)
            vm.Password = pb.Password;
    }
}

