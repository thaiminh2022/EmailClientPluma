using EmailClientPluma.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Windows;

namespace EmailClientPluma.Core.Services;

internal interface IWindowFactory
{
    TView CreateWindow<TView, TViewModel>()
        where TView : Window, new()
        where TViewModel : class;
}

internal class WindowFactory : IWindowFactory
{
    private readonly IServiceProvider _serviceProvider;

    public WindowFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }


    TView IWindowFactory.CreateWindow<TView, TViewModel>()
    {
        var vm = _serviceProvider.GetRequiredService<TViewModel>();
        var window = new TView
        {
            DataContext = vm
        };

        if (vm is IRequestClose rc)
        {
            rc.RequestClose += (_, result) =>
            {
                var asDialog = (bool?)typeof(Window)
                    .GetField("_showingAsDialog", BindingFlags.Instance | BindingFlags.NonPublic)?
                    .GetValue(window);

                if (asDialog is true)
                {
                    window.DialogResult = result;
                }
                window.Close();
            };
        }

        return window;
    }
}