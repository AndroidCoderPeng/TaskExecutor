using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace TaskExecutor.Views;

public partial class FaqView : UserControl
{
    private bool _initialized;

    public FaqView()
    {
        InitializeComponent();
        FaqWebView.Loaded += delegate
        {
            _ = InitWebViewAsync();
        };
    }

    private async Task InitWebViewAsync()
    {
        if (_initialized || FaqWebView.CoreWebView2 != null)
        {
            return;
        }

        LoadingBar.Visibility = Visibility.Visible;

        try
        {
            // 用户数据目录固定到本地应用数据，避免默认落在 exe 目录导致发布目录不可写
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TaskExecutor", "WebView2");

            var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await FaqWebView.EnsureCoreWebView2Async(environment);

            FaqWebView.DefaultBackgroundColor = Color.Transparent; // 让 WPF 卡片圆角透出来

            var coreWebView2 = FaqWebView.CoreWebView2;
            if (coreWebView2 == null)
            {
                ShowFallback();
                return;
            }

            var settings = coreWebView2.Settings;
            settings.AreDefaultContextMenusEnabled = false;
            settings.AreDevToolsEnabled = false;
            settings.IsZoomControlEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.IsBuiltInErrorPageEnabled = false;

            coreWebView2.NavigationCompleted += (_, _) => HideLoading();

            // 加载随程序发布的静态页面
            var htmlPath = Path.Combine(AppContext.BaseDirectory, "DefaultData", "faq.html");
            if (!File.Exists(htmlPath))
            {
                Console.WriteLine($"FAQ 页面文件缺失：{htmlPath}");
                ShowFallback();
                return;
            }

            coreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
            _initialized = true;
        }
        catch (WebView2RuntimeNotFoundException)
        {
            ShowFallback();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAQ 页面初始化失败：{ex.Message}");
            ShowFallback();
        }
    }

    private void HideLoading()
    {
        LoadingBar.Visibility = Visibility.Collapsed;
    }

    private void ShowFallback()
    {
        FaqWebView.Visibility = Visibility.Collapsed;
        FallbackText.Visibility = Visibility.Visible;
        HideLoading();
    }
}