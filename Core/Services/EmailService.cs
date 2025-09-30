using EmailClientPluma.Core.Models;

namespace EmailClientPluma.Core.Services
{
    interface IEmailService
    {
        Task<IEnumerable<Email>> FetchEmailAsync(Account acc);
        Task SendEmailAsync(Account acc, Email email);
    }
    internal class EmailService : IEmailService
    {
        public async Task<IEnumerable<Email>> FetchEmailAsync(Account acc)
        {
            await Task.Delay(1000); // simulate network delay

            List<Email> emails = new List<Email>();
            Random rand = new Random();

            for (int i = 0; i < rand.Next(3, 10); i++)
            {
                emails.Add(new Email(acc.AccountID, $"subject {rand.Next(100)}", "body", "from", "to", []));
            }


            return emails;
        }

        public Task SendEmailAsync(Account acc, Email email)
        {
            throw new NotImplementedException();
        }
    }
}
