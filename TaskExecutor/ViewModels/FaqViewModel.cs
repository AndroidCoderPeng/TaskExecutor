using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using TaskExecutor.Model;

namespace TaskExecutor.ViewModels;

public class FaqViewModel : ObservableObject
{
    public ObservableCollection<FaqItem> FaqItems { get; } = LoadFromResource();

    public FaqViewModel()
    {
        foreach (var item in FaqItems)
        {
            item.PropertyChanged += OnFaqItemPropertyChanged;
        }
    }

    private static ObservableCollection<FaqItem> LoadFromResource()
    {
        try
        {
            var streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/DefaultData/FAQ.json"));
            if (streamInfo != null)
            {
                using var reader = new StreamReader(streamInfo.Stream);
                var items = JsonConvert.DeserializeObject<List<FaqItem>>(reader.ReadToEnd()) ?? [];
                return [.. items];
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"加载 FAQ 数据失败：{e.Message}");
            return [];
        }

        return [];
    }

    private void OnFaqItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FaqItem.IsExpanded) ||
            sender is not FaqItem { IsExpanded: true } expanded)
        {
            return;
        }

        foreach (var other in FaqItems.Where(x => !ReferenceEquals(x, expanded)))
        {
            other.IsExpanded = false; // 收起其他项；IsExpanded=false 会再次触发事件但被上面过滤，无死循环
        }
    }
}