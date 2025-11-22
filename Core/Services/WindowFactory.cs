using EmailClientPluma.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace EmailClientPluma.Core.Services
{
    interface IWindowFactory
    {
        TView CreateWindow<TView, TViewModel>()
            where TView : Window, new()
            where TViewModel : class;
    }
    internal class WindowFactory : IWindowFactory
    {
        private readonly IServiceProvider _serviceProvider;


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
                    window.DialogResult = result;
                    window.Close();
                };
            }

            return window;
        }

        public WindowFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
    }
}
