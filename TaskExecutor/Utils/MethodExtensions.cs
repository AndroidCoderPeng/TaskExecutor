using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TaskExecutor.Services;

namespace TaskExecutor.Utils;

public static class MethodExtensions
{
    public static void AddDialog<TView, TViewModel>(this IServiceCollection services)
        where TView : FrameworkElement
        where TViewModel : class
    {
        services.AddTransient<TView>();
        services.AddTransient<TViewModel>();
        services.AddSingleton(new ViewModelRegistration(typeof(TView), typeof(TViewModel)));
    }
}