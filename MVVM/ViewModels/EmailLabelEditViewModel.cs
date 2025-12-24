using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services.Storaging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Media;

namespace EmailClientPluma.MVVM.ViewModels
{
    class EmailLabelEditViewModel : ObserableObject, IRequestClose
    {
        private IStorageService _storageService;

        public Account SelectedAccount { get; set; }
        public Email SelectedEmail { get; set; }

        public ObservableCollection<LabelItemViewModel> Labels { get; } = new();

        public ICollectionView FilteredLabels { get; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanges();
                FilteredLabels.Refresh();
            }
        }
        private string _searchText = "";

        public RelayCommandAsync SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public void Setup(Account selectedAccount, Email selectedEmail)
        {
            SelectedAccount = selectedAccount;
            SelectedEmail = selectedEmail;

            foreach (var label in SelectedAccount.OwnedLabels)
            {
                Labels.Add(new LabelItemViewModel
                {
                    Id = label.Id,
                    Name = label.Name,
                    IsSelected = SelectedEmail?.Labels.Any(x => x.Id == label.Id) ?? false,
                    Color = label.Color,
                    IsModifiable = label.IsEditable
                });
            }

        }


        public EmailLabelEditViewModel(IStorageService storageService)
        {
            _storageService = storageService;
            FilteredLabels = CollectionViewSource.GetDefaultView(Labels);
            FilteredLabels.Filter = o =>
            {
                if (string.IsNullOrWhiteSpace(SearchText)) return true;
                if (o is not LabelItemViewModel l) return false;
                return l.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
            };

            SaveCommand = new RelayCommandAsync(SaveChanges);
            CancelCommand = new RelayCommand(CancelChanges);
        }


        private void CancelChanges(object? obj)
        {
            RequestClose?.Invoke(this, false);
        }

        public async Task SaveChanges(object? obj)
        {
            foreach (var labelView in Labels)
            {
                if (!labelView.IsModifiable) continue;

                var label = SelectedAccount.OwnedLabels.FirstOrDefault(item => item.Id == labelView.Id);
                if (label is null) continue;


                if (SelectedEmail.Labels.Contains(label))
                {
                    if (!labelView.IsSelected)
                    {
                        // Remove label
                        SelectedEmail.Labels.Remove(label);
                        await _storageService.DeleteEmailLabelAsync(label, SelectedEmail);
                    }
                }
                else
                {
                    if (labelView.IsSelected)
                    {
                        // Add label
                        SelectedEmail.Labels.Add(label);
                    }
                }
            }

            await _storageService.StoreLabelsAsync(SelectedEmail);
            RequestClose?.Invoke(this, true);
        }

        public event EventHandler<bool?>? RequestClose;


#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public EmailLabelEditViewModel()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {

        }

    }
    public class LabelItemViewModel
    {
        public int Id { get; set; } = -1;
        public string Name { get; set; } = "";
        public bool IsSelected { get; set; }     // Bound to CheckBox
        public Color Color { get; set; }
        public bool IsModifiable { get; set; } = true;
    }


}
