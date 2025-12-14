using Dapper;
using EmailClientPluma.Core.Models;
using Microsoft.Data.Sqlite;

namespace EmailClientPluma.Core.Services.Storaging
{
    internal class LabelStorage
    {
        private readonly string _connectionString;
        private SqliteConnection CreateConnection() => new(_connectionString);
        public LabelStorage(string connectionString)
        {
            _connectionString = connectionString;
        }


        public async Task StoreDefaultLabel(Account acc)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var sql = """
                      INSERT INTO LABELS (LABEL_NAME, OWNER_ID, COLOR, IS_EDITABLE)
                      VALUES (@LabelName, @OwnerId, @Color, 0)
                      """;

            foreach (var label in EmailLabel.Labels)
            {
                var id = await connection.ExecuteScalarAsync<int>(sql, new
                {
                    LabelName = label.Name,
                    OwnerId = acc.ProviderUID,
                    Color = Helper.ColorToARGB(label.Color),
                });


            }

        }

        public async Task DeleteLabelAsync(EmailLabel label)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var sql = """
                      DELETE FROM LABELS
                      WHERE ID = @Id
                      """;

            await connection.ExecuteAsync(sql, new
            {
                Id = label.Id
            });
        }

        public async Task StoreLabelAsync(Account acc)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var insertSql = """
                            INSERT INTO LABELS (LABEL_NAME, OWNER_ID, COLOR, IS_EDITABLE)
                            VALUES (@LabelName, @OwnerId, @Color, 1)
                            RETURNING ID;
                            """;

            // this will also run for system label, but they will be ignored due to IsEditable = 0
            var updateSql = """
                      UPDATE LABELS SET LABEL_NAME = @LabelName, COLOR = @Color
                      WHERE ID = @Id AND IS_EDITABLE = TRUE
                      """;

            foreach (var label in acc.OwnedLabels)
            {
                if (label.Id == -1)
                {
                    // new label, insert without ID
                    try
                    {
                        var id = await connection.ExecuteScalarAsync<int>(insertSql, new
                        {
                            LabelName = label.Name,
                            OwnerId = label.OwnerAccountId,
                            Color = Helper.ColorToARGB(label.Color),
                        });
                        label.Id = id;
                    }
                    catch (Exception e)
                    {
                        MessageBoxHelper.Error(e);
                    }
                }
                else
                {
                    try
                    {
                        await connection.ExecuteAsync(updateSql, new
                        {
                            Id = label.Id,
                            LabelName = label.Name,
                            Color = Helper.ColorToARGB(label.Color),
                        });
                    }
                    catch (Exception e)
                    {
                        MessageBoxHelper.Error(e);
                    }

                }
            }
        }

        public async Task StoreLabelsAsync(Email mail)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var sqlFindLabel = """
                               SELECT ID FROM LABELS
                               WHERE OWNER_ID = @OwnerId AND LABEL_NAME = @LabelName
                               """;
            var sql = """
                      INSERT OR IGNORE INTO EMAIL_LABELS (LABEL_ID, EMAIL_ID)
                      VALUES (@LabelId, @EmailId)
                      """;

            foreach (var label in mail.Labels)
            {
                var labelId = await connection.ExecuteScalarAsync(sqlFindLabel, new
                {
                    OwnerId = mail.MessageIdentifiers.OwnerAccountId,
                    LabelName = label.Name,
                });


                if (!int.TryParse(labelId?.ToString(), out var id))
                    continue;

                await connection.ExecuteAsync(sql, new
                {
                    LabelId = id,
                    EmailId = mail.MessageIdentifiers.EmailId
                });
            }
        }

        public async Task DeleteEmailLabelAsync(EmailLabel label, Email mail)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var sql = """
                        DELETE FROM EMAIL_LABELS
                        WHERE LABEL_ID = @LabelId AND EMAIL_ID = @EmailId
                      """;

            await connection.ExecuteAsync(sql, new
            {
                LabelId = label.Id,
                EmailId = mail.MessageIdentifiers.EmailId
            });
        }

        public async Task<IEnumerable<EmailLabel>> GetLabelsAsync(Account acc)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var sql = """
                      SELECT ID, LABEL_NAME, OWNER_ID, COLOR, IS_EDITABLE
                      FROM LABELS
                      WHERE OWNER_ID = @OwnerId
                      """;

            var labels = await connection.QueryAsync<EmailLabelRow>(sql, new
            {
                OwnerId = acc.ProviderUID
            });

            return labels.Select(x =>
            {
                var label = new EmailLabel(x.LABEL_NAME, Helper.ColorFromARGB(x.COLOR), x.IS_EDITABLE)
                {
                    Id = x.ID
                };

                return label;
            });
        }

        public async Task<IEnumerable<EmailLabel>> GetLabelsAsync(Email email)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            var sql = """
                      SELECT ID, LABEL_NAME, OWNER_ID, COLOR, IS_EDITABLE
                      FROM EMAIL_LABELS el
                      JOIN LABELS l ON el.LABEL_ID = l.ID
                      WHERE OWNER_ID = @OwnerId AND el.EMAIL_ID = @EmailId
                      """;

            var labels = await connection.QueryAsync<EmailLabelRow>(sql, new
            {
                OwnerId = email.MessageIdentifiers.OwnerAccountId,
                EmailId = email.MessageIdentifiers.EmailId
            });

            return labels.Select(x =>
            {
                var label = new EmailLabel(x.LABEL_NAME, Helper.ColorFromARGB(x.COLOR), x.IS_EDITABLE)
                {
                    Id = x.ID
                };

                return label;
            });
        }
    }

    record EmailLabelRow
    {
        public required int ID { get; set; }
        public required string LABEL_NAME { get; set; }
        public required string OWNER_ID { get; set; }
        public required int COLOR { get; set; }
        public required bool IS_EDITABLE { get; set; }
    }
}
