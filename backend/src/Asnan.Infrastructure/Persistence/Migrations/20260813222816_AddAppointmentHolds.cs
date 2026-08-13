using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asnan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentHolds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppointmentHolds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DoctorProfileId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PatientUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SlotStartUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SlotEndUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    HoldTokenHash = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActiveSlotKey = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppointmentHolds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppointmentHolds_DoctorProfiles_DoctorProfileId",
                        column: x => x.DoctorProfileId,
                        principalTable: "DoctorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppointmentHolds_Users_PatientUserId",
                        column: x => x.PatientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentHolds_ActiveSlotKey",
                table: "AppointmentHolds",
                column: "ActiveSlotKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentHolds_DoctorProfileId",
                table: "AppointmentHolds",
                column: "DoctorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentHolds_HoldTokenHash",
                table: "AppointmentHolds",
                column: "HoldTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppointmentHolds_PatientUserId",
                table: "AppointmentHolds",
                column: "PatientUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppointmentHolds");
        }
    }
}
