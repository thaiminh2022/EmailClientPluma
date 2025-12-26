using Dapper;
using EmailClientPluma.Core.Models;
using EmailClientPluma.Core.Models.Exceptions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace EmailClientPluma.Core.Services.Storaging;

internal class LabelStorage(string connectionString, ILogger<StorageService> logger)
{
    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection(connectionString);
    }


    public async Task StoreDefaultLabel(Account acc)
    {
        logger.LogInformation("Storing default labels for {email}", acc.Email);
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        var sql = """
                  INSERT INTO LABELS (LABEL_NAME, OWNER_ID, COLOR, IS_EDITABLE)
                  VALUES (@LabelName, @OwnerId, @Color, 0)
                  """;

        foreach (var label in EmailLabel.Labels)
        {
            try
            {
                await connection.ExecuteScalarAsync<int>(sql, new
                {
                    LabelName = label.Name,
                    OwnerId = acc.ProviderUID,
                    Color = Helper.ColorToArgb(label.Color)
                });
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "CANNOT WRITE DEFAULT LABEL, THIS IS A PROGRAM ERROR");
                throw new WriteLabelException(inner: ex);
            }

        }
    }

    public async Task DeleteLabelAsync(EmailLabel label)
    {
        logger.LogInformation("Deleting label with name {name}", label.Name);

        await using var connection = CreateConnection();
        await connection.OpenAsync();

        var sql = """
                  DELETE FROM LABELS
                  WHERE ID = @Id
                  """;
        try
        {
            await connection.ExecuteAsync(sql, new
            {
                label.Id
            });
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "CANNOT DELETE LABELS FULLY, THIS IS A PROGRAM EXCEPTION");
            throw new RemoveLabelException(inner: ex);
        }

    }

    public async Task StoreLabelAsync(Account acc)
    {
        logger.LogInformation("Storing labels for {email}", acc.Email);

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
            logger.LogInformation("Storing label with name: {name}", label.Name);
            if (label.Id == -1)
            {
                // new label, insert without ID
                try
                {
                    var id = await connection.ExecuteScalarAsync<int>(insertSql, new
                    {
                        LabelName = label.Name,
                        OwnerId = label.OwnerAccountId,
                        Color = Helper.ColorToArgb(label.Color)
                    });
                    label.Id = id;
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "CANNOT WRITE LABEL FOR {email}, THIS IS A PROGRAM ERROR", acc.Email);
                    throw new WriteLabelException(inner: ex);
                }
            }
            else
            {
                try
                {
                    await connection.ExecuteAsync(updateSql, new
                    {
                        label.Id,
                        LabelName = label.Name,
                        Color = Helper.ColorToArgb(label.Color)
                    });
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "CANNOT WRITE LABEL FOR {email}, THIS IS A PROGRAM ERROR", acc.Email);
                    throw new WriteLabelException(inner: ex);
                }
            }
        }
    }

    public async Task StoreLabelsAsync(Email mail)
    {
        logger.LogInformation("Storing labels for mail with subject {subject}", mail.MessageParts.Subject);

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
            logger.LogInformation("Storing label with name {name}", label.Name);
            var labelId = await connection.ExecuteScalarAsync(sqlFindLabel, new
            {
                OwnerId = mail.MessageIdentifiers.OwnerAccountId,
                LabelName = label.Name
            });


            if (!int.TryParse(labelId?.ToString(), out var id))
                continue;
            try
            {
                await connection.ExecuteAsync(sql, new
                {
                    LabelId = id,
                    mail.MessageIdentifiers.EmailId
                });
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "CANNOT WRITE LABEL FOR, THIS IS A PROGRAM ERROR");
                throw new WriteLabelException(inner: ex);
            }

        }
    }

    public async Task DeleteEmailLabelAsync(EmailLabel label, Email mail)
    {
        logger.LogInformation("Deleting label with name {label} of email", label.Name);
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        var sql = """
                    DELETE FROM EMAIL_LABELS
                    WHERE LABEL_ID = @LabelId AND EMAIL_ID = @EmailId
                  """;
        try
        {
            await connection.ExecuteAsync(sql, new
            {
                LabelId = label.Id,
                mail.MessageIdentifiers.EmailId
            });
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "CANNOT REMOVE LABEL, THIS IS A PROGRAM ERROR");
            throw new RemoveLabelException(inner: ex);
        }

    }

    public async Task<IEnumerable<EmailLabel>> GetLabelsAsync(Account acc)
    {
        logger.LogInformation("Reading labels for {email}", acc.Email);
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        var sql = """
                  SELECT ID, LABEL_NAME, OWNER_ID, COLOR, IS_EDITABLE
                  FROM LABELS
                  WHERE OWNER_ID = @OwnerId
                  """;
        try
        {
            var labels = await connection.QueryAsync<EmailLabelRow>(sql, new
            {
                OwnerId = acc.ProviderUID
            });
            return labels.Select(x =>
            {
                var label = new EmailLabel(x.LABEL_NAME, Helper.ColorFromArgb(x.COLOR), x.IS_EDITABLE)
                {
                    Id = x.ID
                };

                return label;
            });
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "CANNOT READ LABELS, THIS IS A PROGRAM ERROR");
            throw new ReadLabelException(inner: ex);
        }
    }

    public async Task<IEnumerable<EmailLabel>> GetLabelsAsync(Email email)
    {
        logger.LogInformation("Reading labels for email with subject {subject}", email.MessageParts.Subject);
        await using var connection = CreateConnection();
        await connection.OpenAsync();

        var sql = """
                  SELECT ID, LABEL_NAME, OWNER_ID, COLOR, IS_EDITABLE
                  FROM EMAIL_LABELS el
                  JOIN LABELS l ON el.LABEL_ID = l.ID
                  WHERE OWNER_ID = @OwnerId AND el.EMAIL_ID = @EmailId
                  """;
        try
        {
            var labels = await connection.QueryAsync<EmailLabelRow>(sql, new
            {
                OwnerId = email.MessageIdentifiers.OwnerAccountId,
                email.MessageIdentifiers.EmailId
            });

            return labels.Select(x =>
            {
                var label = new EmailLabel(x.LABEL_NAME, Helper.ColorFromArgb(x.COLOR), x.IS_EDITABLE)
                {
                    Id = x.ID
                };

                return label;
            });
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "CANNOT READ LABELS, THIS IS A PROGRAM ERROR");
            throw new ReadEmailException(inner: ex);
        }

    }
}

internal record EmailLabelRow
{
    public required int ID { get; init; }
    public required string LABEL_NAME { get; init; }
    public required string OWNER_ID { get; init; }
    public required int COLOR { get; init; }
    public required bool IS_EDITABLE { get; init; }
}