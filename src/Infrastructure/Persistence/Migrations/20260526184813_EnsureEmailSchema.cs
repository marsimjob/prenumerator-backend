using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnsureEmailSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Username' AND object_id = OBJECT_ID('Users'))
                    DROP INDEX IX_Users_Username ON Users;
                IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'Username' AND Object_ID = OBJECT_ID('Users'))
                    ALTER TABLE Users DROP COLUMN Username;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'Email' AND Object_ID = OBJECT_ID('Users'))
                    ALTER TABLE Users ADD Email nvarchar(256) NOT NULL DEFAULT '';
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'IsEmailVerified' AND Object_ID = OBJECT_ID('Users'))
                    ALTER TABLE Users ADD IsEmailVerified bit NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'VerificationCode' AND Object_ID = OBJECT_ID('Users'))
                    ALTER TABLE Users ADD VerificationCode nvarchar(8) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = 'VerificationCodeExpiry' AND Object_ID = OBJECT_ID('Users'))
                    ALTER TABLE Users ADD VerificationCodeExpiry datetime2 NULL;
            ");
            migrationBuilder.Sql(
                "UPDATE Users SET Email = CONCAT('legacy_', CAST(Id AS NVARCHAR(36)), '@placeholder.invalid') WHERE Email = ''");
            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_Email' AND object_id = OBJECT_ID('Users')) CREATE UNIQUE INDEX IX_Users_Email ON Users (Email)");
        }

        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
