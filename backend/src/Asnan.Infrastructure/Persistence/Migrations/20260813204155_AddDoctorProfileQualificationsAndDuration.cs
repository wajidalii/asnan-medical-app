using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asnan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDoctorProfileQualificationsAndDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AppointmentDurationMinutes",
                table: "DoctorProfiles",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "Qualifications",
                table: "DoctorProfiles",
                type: "varchar(1024)",
                maxLength: 1024,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppointmentDurationMinutes",
                table: "DoctorProfiles");

            migrationBuilder.DropColumn(
                name: "Qualifications",
                table: "DoctorProfiles");
        }
    }
}
