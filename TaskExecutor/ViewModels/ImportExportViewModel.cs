using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TaskExecutor.ViewModels;

public partial class ImportExportViewModel : ObservableObject
{
    #region VM

    [ObservableProperty] private string _exportFileName = string.Empty;

    #endregion

    private readonly DispatcherTimer _dateRefreshTimer = new();
    
    public ImportExportViewModel()
    {
        RefreshExportFileName();
        _dateRefreshTimer.Tick += OnDateRefreshTimerTick;
    }
    
    private void OnDateRefreshTimerTick(object? sender, EventArgs e)
    {
        _dateRefreshTimer.Stop();
        RefreshExportFileName();
        ScheduleNextMidnight();
    }
    
    private void RefreshExportFileName()
    {
        var date = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        ExportFileName = $"TaskExecutor_备份_{date}.json";
    }
    
    private void ScheduleNextMidnight()
    {
        _dateRefreshTimer.Stop();
        var now = DateTime.Now;
        _dateRefreshTimer.Interval = now.Date.AddDays(1) - now;
        _dateRefreshTimer.Start();
    }
    
    public void StartDateRefresh()
    {
        RefreshExportFileName();
        ScheduleNextMidnight();
    }
    
    public void StopDateRefresh()
    {
        _dateRefreshTimer.Stop();
    }
}