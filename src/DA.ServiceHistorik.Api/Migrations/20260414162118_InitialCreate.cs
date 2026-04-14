using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DA.ServiceHistorik.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SentReminders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LicensePlate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReminderType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ServiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SentReminders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SentReminders_LicensePlate_ReminderType_ServiceDate",
                table: "SentReminders",
                columns: new[] { "LicensePlate", "ReminderType", "ServiceDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SentReminders");
        }
    }
}
