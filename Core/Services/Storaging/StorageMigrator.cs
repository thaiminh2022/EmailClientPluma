using Dapper;
using EmailClientPluma.Core.Models;
using Microsoft.Data.Sqlite;

namespace EmailClientPluma.Core.Services.Storaging
{
    internal sealed class StorageMigrator(string connectionString)
    {
        private readonly string _connectionString = connectionString;

        private SqliteConnection CreateConnection() => new SqliteConnection(_connectionString);

        public void Migrate()
        {
            using var connection = CreateConnection();
            connection.Open();

            connection.Execute("PRAGMA foreign_keys = ON;");
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

            if (currentVersion < 3)
            {
                ApplyV3_Label(connection);
                SetVersion(connection, 3);
                currentVersion = 3;
            }
        }


        #region // ---------------- SchemaVersion helpers ----------------

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
            var version = connection.ExecuteScalar<long?>(
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

        #endregion

        #region // ---------------- v1: base schema ----------------

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

            connection.Execute(createAccountsSql);
            connection.Execute(createEmailsSql);
        }

        #endregion

        #region // ---------------- v2: add unique infos ----------------

        private void ApplyV2_BetterUnique(SqliteConnection connection)
        {
            // 1) Create new table with correct schema

            using var tx = connection.BeginTransaction();
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

            var copyDataSql = @" INSERT INTO EMAILS_NEW (
                                    EMAIL_ID,
                                    IMAP_UID,
                                    IMAP_UID_VALIDITY,
                                    FOLDER_FULLNAME,
                                    MESSAGE_ID,
                                    OWNER_ID,
                                    IN_REPLY_TO,
                                    SUBJECT,
                                    BODY,
                                    FROM_ADDRESS,
                                    TO_ADDRESS,
                                    DATE
                                )
                                SELECT
                                    EMAIL_ID,
                                    IMAP_UID,
                                    IMAP_UID_VALIDITY,
                                    FOLDER_FULLNAME,
                                    MESSAGE_ID,
                                    OWNER_ID,
                                    IN_REPLY_TO,
                                    SUBJECT,
                                    BODY,
                                    FROM_ADDRESS,
                                    TO_ADDRESS,
                                    DATE
                                FROM EMAILS;
                                    ";

            connection.Execute(createNewTableSql, transaction: tx);
            connection.Execute(copyDataSql, transaction: tx);

            // 3) Drop old table and rename new one
            connection.Execute("DROP TABLE EMAILS;", transaction: tx);
            connection.Execute("ALTER TABLE EMAILS_NEW RENAME TO EMAILS;", transaction: tx);

            tx.Commit();
        }

        #endregion


        #region // ---------------- v3: add label ----------------

        private void ApplyV3_Label(SqliteConnection connection)
        {
            using var tx = connection.BeginTransaction();

            // add read to emails
            connection.Execute(@"ALTER TABLE EMAILS ADD COLUMN FLAGS INTEGER NOT NULL DEFAULT 0;", transaction: tx);


            // label table
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS LABELS (
                    ID              INTEGER PRIMARY KEY AUTOINCREMENT,
                    LABEL_NAME      TEXT,
                    OWNER_ID        TEXT,                   -- if this is null, it's a global label
                    COLOR           INT,
                    IS_EDITABLE    BOOLEAN NOT NULL DEFAULT 1,
                    FOREIGN KEY (OWNER_ID) REFERENCES ACCOUNTS(PROVIDER_UID) ON DELETE CASCADE
                );", transaction: tx);

            // emails label table
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS EMAIL_LABELS (
                    LABEL_ID        INTEGER NOT NULL,
                    EMAIL_ID        INTEGER NOT NULL,
                    PRIMARY KEY (EMAIL_ID, LABEL_ID),
                    FOREIGN KEY (EMAIL_ID) REFERENCES EMAILS(EMAIL_ID) ON DELETE CASCADE,
                    FOREIGN KEY (LABEL_ID) REFERENCES LABELS(ID) ON DELETE CASCADE
                );", transaction: tx);

            

            tx.Commit();
        }

        #endregion
    }
}