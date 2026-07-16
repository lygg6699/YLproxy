using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YLproxy.Models;
using DataGrid = System.Windows.Controls.DataGrid;
using UserControl = System.Windows.Controls.UserControl;

namespace YLproxy.GUI.Views;

public partial class MainView : UserControl
{
    private MainViewModel? _subscribedVm;

    public MainView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedVm is not null)
            _subscribedVm.FilteredLogs.CollectionChanged -= OnFilteredLogsChanged;

        _subscribedVm = e.NewValue as MainViewModel;

        if (_subscribedVm is not null)
            _subscribedVm.FilteredLogs.CollectionChanged += OnFilteredLogsChanged;
    }

    private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.SelectedProxies = ((DataGrid)sender).SelectedItems
                .Cast<ProxyItem>().ToList();
        }
    }

    private void OnFilteredLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        if (LogListView.Items.Count == 0) return;

        var scrollViewer = FindScrollViewer(LogListView);
        if (scrollViewer is null) return;

        var isAtBottom = scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 30;

        if (isAtBottom)
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                LogListView.ScrollIntoView(LogListView.Items[^1]);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer sv) return sv;
            var result = FindScrollViewer(child);
            if (result is not null) return result;
        }
        return null;
    }
}
