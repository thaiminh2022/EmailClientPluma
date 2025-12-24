using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services.Accounting;
using EmailClientPluma.Core.Services.Storaging;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace EmailClientPluma.MVVM.ViewModels
{
    internal class LabelEditorViewModel : ObserableObject, IRequestClose
    {
        public event EventHandler<bool?>? RequestClose;

        private readonly IStorageService _storageService;

        // --- Data ---
        public ObservableCollection<Account> Accounts { get; set; }


        private readonly List<EmailLabel> _newLabels = [];
        private readonly List<EmailLabel> _deletedLabels = [];

        // A preset list of colors for the UI color picker
        public ObservableCollection<Color> AvailableColors { get; } =
        [
            Colors.Red, Colors.Orange, Colors.Gold, Colors.Green,
            Colors.Teal, Colors.DodgerBlue, Colors.Purple, Colors.SlateGray
        ];

        // --- Selection State ---
        private Account? _selectedAccount;
        public Account? SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                _selectedAccount = value;
                OnPropertyChanges();
                // When account changes, clear label selection
                SelectedLabel = null;
            }
        }


        private EmailLabel? _selectedLabel;
        public EmailLabel? SelectedLabel
        {
            get => _selectedLabel;
            set
            {
                _selectedLabel = value;

                // Determine if we can edit this label
                if (value is null) IsEditEnabled = false;
                else
                {
                    IsEditEnabled = value.IsEditable;
                }

                OnPropertyChanges();
                DeleteLabelCommand?.RaiseCanExecuteChanged();
            }
        }

        private bool _isEditEnabled;
        public bool IsEditEnabled
        {
            get => _isEditEnabled;
            set { _isEditEnabled = value; OnPropertyChanges(); }
        }

        // --- Commands ---
        public RelayCommand AddLabelCommand { get; }
        public RelayCommand? DeleteLabelCommand { get; }

        public RelayCommandAsync SaveChangesCommand { get; }
        public RelayCommand CancelChangesCommand { get; }


        // --- Constructor ---
        public LabelEditorViewModel(IAccountService accountService, IStorageService storageService)
        {
            _storageService = storageService;
            Accounts = accountService.GetAccounts();

            // Default select first account if available
            if (Accounts.Count > 0) SelectedAccount = Accounts[0];

            AddLabelCommand = new RelayCommand(ExecuteAddLabel);
            DeleteLabelCommand = new RelayCommand(ExecuteDeleteLabel, CanDeleteLabel);
            SaveChangesCommand = new RelayCommandAsync(SaveChanges, _ => _deletedLabels.Count > 0 || _newLabels.Count > 0);
            CancelChangesCommand = new RelayCommand(CancelChanges);
        }

        private async Task SaveChanges(object? obj)
        {
            if (SelectedAccount is null)
            {
                RequestClose?.Invoke(this, false);
                return;
            }
            // Persist new labels
            await _storageService.StoreLabelAsync(SelectedAccount);

            foreach (var label in _deletedLabels)
            {
                await _storageService.DeleteLabelAsync(label);
            }

            RequestClose?.Invoke(this, true);
        }

        private void CancelChanges(object? obj)
        {
            if (SelectedAccount is null)
            {
                RequestClose?.Invoke(this, false);
                return;
            }

            foreach (var label in _newLabels)
            {
                SelectedAccount.OwnedLabels.Remove(label);
            }

            RequestClose?.Invoke(this, true);
        }


        private void ExecuteAddLabel(object? obj)
        {
            if (SelectedAccount is null) return;

            var newLabel = new EmailLabel("New Label", Colors.SlateGray, true)
            {
                OwnerAccountId = SelectedAccount.ProviderUID,
            };

            _newLabels.Add(newLabel);

            SelectedAccount.OwnedLabels.Add(newLabel);
            SelectedLabel = newLabel;
        }

        private void ExecuteDeleteLabel(object? obj)
        {
            if (SelectedAccount is null || SelectedLabel is null) return;

            if (_newLabels.Contains(SelectedLabel))
            {
                _newLabels.Remove(SelectedLabel);
            }

            _deletedLabels.Add(SelectedLabel);
            SelectedAccount.OwnedLabels.Remove(SelectedLabel);

            SelectedLabel = null;
        }

        private bool CanDeleteLabel(object? obj)
        {
            return SelectedLabel != null && IsEditEnabled;
        }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public LabelEditorViewModel()
        {
        } // Design-time constructor
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    }


}