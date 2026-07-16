using System.Linq;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;
using DataGrid = System.Windows.Controls.DataGrid;

namespace YLproxy.GUI.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.SelectedProxies = ((DataGrid)sender).SelectedItems.Cast<YLproxy.Models.ProxyItem>().ToList();
        }
    }
}
