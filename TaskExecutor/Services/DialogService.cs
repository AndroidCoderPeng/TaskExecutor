using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace TaskExecutor.Services;

public sealed record ViewModelRegistration(Type ViewType, Type ViewModelType);

public interface IDialogParameterReceiver
{
    void OnDialogOpened(object? parameter);
}

public interface IDialogCloseRequester
{
    event Action<bool> CloseRequested;
}

public sealed class DialogService : IDialogService
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<Type, Type> _registrations;
    
    public DialogService(IServiceProvider services, IEnumerable<ViewModelRegistration> registrations)
    {
        _services = services;
        _registrations = registrations.ToDictionary(register => register.ViewType, register => register.ViewModelType);
    }
    
    public bool? ShowDialog(Type viewType, object? parameter = null)
    {
        return CreateWindow(viewType, parameter).ShowDialog();
    }

    private Window CreateWindow(Type viewType, object? parameter)
    {
        if (!_registrations.TryGetValue(viewType, out var viewModelType))
        {
            throw new ArgumentException($"未注册 View 类型“{viewType.FullName}”。", nameof(viewType));
        }
        
        var view = (FrameworkElement)_services.GetRequiredService(viewType);
        var viewModel = _services.GetRequiredService(viewModelType);
        view.DataContext = viewModel;
        
        if (viewModel is IDialogParameterReceiver receiver)
        {
            receiver.OnDialogOpened(parameter);
        }
        
        Window window;
        if (view is Window dialogWindow)
        {
            window = dialogWindow;
            if (Application.Current.MainWindow is { } mainWindow && !ReferenceEquals(window, mainWindow))
            {
                window.Owner = mainWindow;
            }
        }
        else
        {
            window = new Window
            {
                Owner = Application.Current.MainWindow,
                Content = view
            };
        }
        
        if (viewModel is IDialogCloseRequester closeRequester)
        {
            Action<bool> closeHandler = result => window.DialogResult = result;
            closeRequester.CloseRequested += closeHandler;
            window.Closed += (_, _) => closeRequester.CloseRequested -= closeHandler;
        }

        return window;
    }
}