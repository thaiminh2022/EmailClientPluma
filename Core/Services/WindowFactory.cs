using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace EmailClientPluma.Core.Services
{
    interface IWindowFactory {
        TView CreateWindow<TView, TViewModel>()
            where TView : Window, new()
            where TViewModel : class;
    }
    internal class WindowFactory : IWindowFactory
    {
        private readonly IServiceProvider _serviceProvider;


        TView IWindowFactory.CreateWindow<TView, TViewModel>()
        {
            return new TView
            {
                DataContext = _serviceProvider.GetRequiredService<TViewModel>()
            };
        }

        public WindowFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
    }
}
