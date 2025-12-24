using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services;
using EmailClientPluma.MVVM.Views;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Emailing;

namespace EmailClientPluma.MVVM.ViewModels
{
    internal class WhichProvViewModel
    {
        readonly IAccountService _accountService;
        public RelayCommand AddAccountCommand_GG { get; set; }

        public WhichProvViewModel(IAccountService accountService)
        {
            _accountService = accountService;

            AddAccountCommand_GG = new RelayCommand(async _ =>
            {
                // TODO: ADd more provider
                await _accountService.AddAccountAsync(Provider.Google);
            });
        }
    }
}
