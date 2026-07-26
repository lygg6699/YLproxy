using System.Windows;
using YLproxy.GUI.ViewModels;

namespace YLproxy.GUI.Views;

public partial class ManageGroupsWindow : Window
{
    public ManageGroupsWindow()
    {
        InitializeComponent();
    }

    private ManageGroupsViewModel? _viewModel;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _viewModel = DataContext as ManageGroupsViewModel;
    }
}

