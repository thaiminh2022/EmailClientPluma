using EmailClientPluma.Core;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Services.Storaging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Media;

namespace EmailClientPluma.MVVM.ViewModels;

internal class EmailLabelEditViewModel : ObserableObject, IRequestClose
{
    private readonly IStorageService _storageService;
    private string _searchText = "";
    private Account? _selectedAccount { get; set; }
    private Email? _selectedEmail { get; set; }

    private ObservableCollection<LabelItemViewModel> _labels { get; } = [];

    public EmailLabelEditViewModel(IStorageService storageService)
    {
        _storageService = storageService;
        FilteredLabels = CollectionViewSource.GetDefaultView(_labels);
        FilteredLabels.Filter = o =>
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return true;
            return o is LabelItemViewModel l && l.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        };

        SaveCommand = new RelayCommandAsync(SaveChanges);
        CancelCommand = new RelayCommand(CancelChanges);
    }


#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public EmailLabelEditViewModel()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    {
    }



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

    public RelayCommandAsync SaveCommand { get; }
    public RelayCommand CancelCommand { get; }

    public event EventHandler<bool?>? RequestClose;

    public void Setup(Account selectedAccount, Email selectedEmail)
    {
        _selectedAccount = selectedAccount;
        _selectedEmail = selectedEmail;

        foreach (var label in _selectedAccount.OwnedLabels)
            _labels.Add(new LabelItemViewModel
            {
                Id = label.Id,
                Name = label.Name,
                IsSelected = _selectedEmail?.Labels.Any(x => x.Id == label.Id) ?? false,
                Color = label.Color,
                IsModifiable = label.IsEditable
            });
    }


    private void CancelChanges(object? obj)
    {
        RequestClose?.Invoke(this, false);
    }

    private async Task SaveChanges(object? obj)
    {
        try
        {
            foreach (var labelView in _labels)
            {
                if (!labelView.IsModifiable) continue;

                var label = _selectedAccount.OwnedLabels.FirstOrDefault(item => item.Id == labelView.Id);
                if (label is null) continue;

                if (_selectedEmail.Labels.Contains(label))
                {
                    if (labelView.IsSelected) continue;

                    // Remove label
                    _selectedEmail.Labels.Remove(label);
                    await _storageService.DeleteEmailLabelAsync(label, _selectedEmail);
                }
                else
                {
                    if (labelView.IsSelected)
                    {
                        // Add label
                        _selectedEmail.Labels.Add(label);
                    }
                }
            }

            await _storageService.StoreLabelsAsync(_selectedEmail);
            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            MessageBoxHelper.Error(ex.Message);
        }

    }
}

public class LabelItemViewModel
{
    public int Id { get; init; } = -1;
    public string Name { get; init; } = "";
    public bool IsSelected { get; init; } // Bound to CheckBox
    public Color Color { get; set; }
    public bool IsModifiable { get; init; } = true;
}