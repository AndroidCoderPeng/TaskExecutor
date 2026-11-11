using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TaskExecutor.Views;

public partial class TaskManagerView : UserControl
{
    public TaskManagerView()
    {
        InitializeComponent();
        
        // WPF DataGrid 默认右键不会改变选中行。如果用户先点了 A 行、再右键 B 行，SelectedItem 还是 A，菜单就会作用到错误的任务上。
        TaskDataGrid.PreviewMouseRightButtonDown += TaskDataGrid_PreviewMouseRightButtonDown;
    }

    private void TaskDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement(TaskDataGrid, source) is DataGridRow { IsSelected: false } row)
        {
            TaskDataGrid.SelectedItem = row.Item;
        }
    }
}