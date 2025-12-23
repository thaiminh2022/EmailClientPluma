using Dapper;
using Microsoft.Data.Sqlite;

namespace EmailClientPluma.Core.Services.Storaging
{
    internal sealed class StorageMigratior
    {
        private readonly string _connectionString;

        public StorageMigratior(string connectionString)
        {
            _connectionString = connectionString;
        }

        private SqliteConnection CreateConnection() => new SqliteConnection(_connectionString);

        public void Migrate()
        {
            using var connection = CreateConnection();
            connection.Open();

            EnsureSchemaVersionTable(connection);

            var currentVersion = GetCurrentVersion(connection);

            if (currentVersion < 1)
            {
                ApplyV1_CreateBaseSchema(connection);
                SetVersion(connection, 1);
                currentVersion = 1;
            }

            // use this for version 2
            if (currentVersion < 2)
            {
                ApplyV2_BetterUnique(connection);
                SetVersion(connection, 2);
                currentVersion = 2;
            }
        }

        // ---------------- SchemaVersion helpers ----------------

        private void EnsureSchemaVersionTable(SqliteConnection connection)
        {
            const string sql = @"
                                CREATE TABLE IF NOT EXISTS SchemaVersion (
                                    Id      INTEGER PRIMARY KEY CHECK (Id = 1),
                                    Version INTEGER NOT NULL
                                );
                               ";

            connection.Execute(sql);
        }

        private long GetCurrentVersion(SqliteConnection connection)
        {
            var version = connection.ExecuteScalar<int?>(
                "SELECT Version FROM SchemaVersion WHERE Id = 1;");

            return version ?? 0L;
        }

        private void SetVersion(SqliteConnection connection, long version)
        {
            const string sql = @"
                                INSERT INTO SchemaVersion (Id, Version)
                                VALUES (1, @Version)
                                ON CONFLICT(Id) DO UPDATE SET Version = @Version;
                                ";

            connection.Execute(sql, new { Version = version });
        }

        // ---------------- v1: base schema ----------------

        private void ApplyV1_CreateBaseSchema(SqliteConnection connection)
        {
            const string createAccountsSql = @"
                        CREATE TABLE IF NOT EXISTS ACCOUNTS (
                            PROVIDER_UID     TEXT PRIMARY KEY NOT NULL,             
                            PROVIDER         TEXT NOT NULL,            
                            EMAIL            TEXT NOT NULL,
                            DISPLAY_NAME     TEXT,
                            UNIQUE (PROVIDER, PROVIDER_UID, EMAIL)
                        );";

            const string createEmailsSql = @"
                        CREATE TABLE IF NOT EXISTS EMAILS (
                            EMAIL_ID           INTEGER PRIMARY KEY AUTOINCREMENT,

                            IMAP_UID           INTEGER NOT NULL,
                            IMAP_UID_VALIDITY  INTEGER NOT NULL,
                            FOLDER_FULLNAME    TEXT    NOT NULL,
                            MESSAGE_ID         TEXT    UNIQUE,
                            OWNER_ID           TEXT    NOT NULL,
                            IN_REPLY_TO        TEXT,

                            SUBJECT            TEXT    NOT NULL,
                            BODY               TEXT,
                            FROM_ADDRESS       TEXT    NOT NULL,
                            TO_ADDRESS         TEXT    NOT NULL,
                            DATE               TEXT,

                            FOREIGN KEY (OWNER_ID) REFERENCES ACCOUNTS(PROVIDER_UID) ON DELETE CASCADE
                        );";

            const string createAttachmentsSql = @"
                        CREATE TABLE IF NOT EXISTS ATTACHMENTS (
                            ATTACHMENT_ID   INTEGER PRIMARY KEY AUTOINCREMENT,
                            EMAIL_ID        INTEGER NOT NULL,
                            FILENAME        TEXT    NOT NULL,
                            MIMETYPE       TEXT    NOT NULL,
                            SIZE            INTEGER NOT NULL,
                            STORAGE_KEY     TEXT    NOT NULL,
                            CREATEDUTC     TEXT    NOT NULL,
                            FOREIGN KEY (EMAIL_ID) REFERENCES EMAILS(EMAIL_ID) ON DELETE CASCADE
                        );";

            connection.Execute(createAccountsSql);
            connection.Execute(createEmailsSql);
            connection.Execute(createAttachmentsSql);
        }

        // ---------------- v2: add flags / cache columns ----------------

        private void ApplyV2_BetterUnique(SqliteConnection connection)
        {
            // We assume we're on v2 schema: EMAILS already has the flag columns.
            // 1) Create new table with correct schema
            var createNewTableSql = @"
                        CREATE TABLE IF NOT EXISTS EMAILS_NEW (
                            EMAIL_ID           INTEGER PRIMARY KEY AUTOINCREMENT,

                            IMAP_UID           INTEGER NOT NULL,
                            IMAP_UID_VALIDITY  INTEGER NOT NULL,
                            FOLDER_FULLNAME    TEXT    NOT NULL,
                            MESSAGE_ID         TEXT,
                            OWNER_ID           TEXT    NOT NULL,
                            IN_REPLY_TO        TEXT,

                            SUBJECT            TEXT    NOT NULL,
                            BODY               TEXT,
                            FROM_ADDRESS       TEXT    NOT NULL,
                            TO_ADDRESS         TEXT    NOT NULL,
                            DATE               TEXT,

                            FOREIGN KEY (OWNER_ID) REFERENCES ACCOUNTS(PROVIDER_UID) ON DELETE CASCADE,

                            -- NEW: one row per account+folder+UIDVALIDITY+UID
                            UNIQUE (OWNER_ID, FOLDER_FULLNAME, IMAP_UID_VALIDITY, IMAP_UID)
                        );
                        ";

            connection.Execute(createNewTableSql);

            var copyDataSql = @"INSERT INTO EMAILS_NEW SELECT * FROM EMAILS;";

            connection.Execute(copyDataSql);

            // 3) Drop old table and rename new one
            connection.Execute("DROP TABLE EMAILS;");
            connection.Execute("ALTER TABLE EMAILS_NEW RENAME TO EMAILS;");
        }
    }
}


