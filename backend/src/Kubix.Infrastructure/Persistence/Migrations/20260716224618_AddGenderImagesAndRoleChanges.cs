using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kubix.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGenderImagesAndRoleChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "image",
                table: "vehicles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gender",
                table: "users",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "profile_image",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gender",
                table: "registration_requests",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "profile_image",
                table: "registration_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_gender",
                table: "users",
                sql: "gender IS NULL OR gender IN ('male', 'female')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_registration_requests_gender",
                table: "registration_requests",
                sql: "gender IS NULL OR gender IN ('male', 'female')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_gender",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_registration_requests_gender",
                table: "registration_requests");

            migrationBuilder.DropColumn(
                name: "image",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "gender",
                table: "users");

            migrationBuilder.DropColumn(
                name: "profile_image",
                table: "users");

            migrationBuilder.DropColumn(
                name: "gender",
                table: "registration_requests");

            migrationBuilder.DropColumn(
                name: "profile_image",
                table: "registration_requests");
        }
    }
}
