using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;

using EmailClientPluma.Core.Services.Accounting;

namespace EmailClientPluma.MVVM.ViewModels
{
    internal class WhichProvViewModel : IRequestClose
    {
        public RelayCommandAsync AddAccountGoogleCommand { get; set; }
        public RelayCommandAsync AddAccountMicrosoftCommand { get; set; }

        public WhichProvViewModel(IAccountService accountService)
        {
            var accountService1 = accountService;

            AddAccountGoogleCommand = new RelayCommandAsync(async _ =>
            {
                await accountService1.AddAccountAsync(Provider.Google);
                RequestClose?.Invoke(this, true);
            });

            AddAccountMicrosoftCommand = new RelayCommandAsync(async _ =>
            {
                await accountService1.AddAccountAsync(Provider.Microsoft);
                RequestClose?.Invoke(this, true);
            });
        }

        public event EventHandler<bool?>? RequestClose;


#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public WhichProvViewModel() {}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    }
}
