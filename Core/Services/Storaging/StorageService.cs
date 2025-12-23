using EmailClientPluma.Core.Models;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace EmailClientPluma.Core.Services.Storaging;

internal interface IStorageService
{
    Task<IEnumerable<Account>> GetAccountsAsync();
    Task<int> StoreAccountAsync(Account account);
    Task RemoveAccountAsync(Account account);
    Task UpdatePaginationAndNextTokenAsync(Account account);

    Task<IEnumerable<Email>> GetEmailsAsync(Account acc);
    Task StoreEmailAsync(Account acc);
    Task StoreEmailAsync(Account acc, Email mail);
    Task UpdateEmailBodyAsync(Email email);

    Task<IEnumerable<EmailLabel>> GetLabelsAsync(Account acc);
    Task StoreLabelAsync(Account acc);
    Task StoreLabelsAsync(Email mail);
    Task DeleteLabelAsync(EmailLabel label);
    Task DeleteEmailLabelAsync(EmailLabel label, Email email);
}

internal class StorageService : IStorageService
{
    private readonly AccountStorage _accountStorage;
    private readonly EmailStorage _emailStorage;
    private readonly LabelStorage _labelStorage;

    public StorageService(ILogger<StorageService> logger)
    {
        var connectionString = $"Data Source={Helper.DatabasePath}";
        var tokenStore = new GoogleDataStore(Helper.DatabasePath);

        _accountStorage = new AccountStorage(tokenStore, connectionString, logger);
        _emailStorage = new EmailStorage(connectionString, logger);
        _labelStorage = new LabelStorage(connectionString, logger);

        var migrator = new StorageMigrator(connectionString);
        migrator.Migrate();
    }


    #region Accounts

    public async Task<IEnumerable<Account>> GetAccountsAsync()
    {
        return await _accountStorage.GetAccountsAsync();
    }

    public async Task<int> StoreAccountAsync(Account account)
    {
        var affected = await _accountStorage.StoreAccountAsync(account);
        await _labelStorage.StoreDefaultLabel(account);
        return affected;
    }

    public async Task RemoveAccountAsync(Account account)
    {
        await _accountStorage.RemoveAccountAsync(account);
    }

    public async Task UpdatePaginationAndNextTokenAsync(Account account)
    {
        await _accountStorage.UpdatePaginationAndNextTokenAsync(account);
    }

    #endregion

    #region Emails

    public async Task StoreEmailsInternal(Account acc, IEnumerable<Email> mails)
    {
        var mailAsync = mails.ToList();

        // Store emails
        await _emailStorage.StoreEmailsInternal(acc, mailAsync);

        foreach (var mail in mailAsync) await _labelStorage.StoreLabelsAsync(mail);
    }

    public async Task StoreEmailAsync(Account acc)
    {
        await StoreEmailsInternal(acc, acc.Emails);
    }

    public async Task StoreEmailAsync(Account acc, Email mail)
    {
        await StoreEmailsInternal(acc, [mail]);
    }

    public async Task<IEnumerable<Email>> GetEmailsAsync(Account acc)
    {
        var emails = await _emailStorage.GetEmailsAsync(acc);

        foreach (var mail in emails)
        {
            var labels = await _labelStorage.GetLabelsAsync(mail);
            mail.Labels = new ObservableCollection<EmailLabel>(labels);
        }

        return emails;
    }

    public async Task UpdateEmailBodyAsync(Email email)
    {
        await _emailStorage.UpdateEmailBodyAsync(email);
    }

    #endregion

    #region Labels

    public async Task<IEnumerable<EmailLabel>> GetLabelsAsync(Account acc)
    {
        return await _labelStorage.GetLabelsAsync(acc);
    }

    public async Task StoreLabelAsync(Account acc)
    {
        await _labelStorage.StoreLabelAsync(acc);
    }

    public async Task StoreLabelsAsync(Email mail)
    {
        await _labelStorage.StoreLabelsAsync(mail);
    }

    public async Task DeleteLabelAsync(EmailLabel label)
    {
        await _labelStorage.DeleteLabelAsync(label);
    }

    public async Task DeleteEmailLabelAsync(EmailLabel label, Email email)
    {
        await _labelStorage.DeleteEmailLabelAsync(label, email);
    }

    #endregion
}