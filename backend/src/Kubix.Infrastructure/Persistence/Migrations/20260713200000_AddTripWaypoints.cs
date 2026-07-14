using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kubix.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public class AddTripWaypoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "distance_to_route_m",
                table: "ride_requests",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "suggested_lat",
                table: "ride_requests",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "suggested_lng",
                table: "ride_requests",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "trip_waypoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false),
                    lat = table.Column<double>(type: "double precision", nullable: false),
                    lng = table.Column<double>(type: "double precision", nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_waypoints", x => x.id);
                    table.ForeignKey(
                        name: "FK_trip_waypoints_trips_trip_id",
                        column: x => x.trip_id,
                        principalTable: "trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_trip_waypoints_universities_university_id",
                        column: x => x.university_id,
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trip_waypoints_trip_id_seq",
                table: "trip_waypoints",
                columns: new[] { "trip_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trip_waypoints_university_id",
                table: "trip_waypoints",
                column: "university_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trip_waypoints");

            migrationBuilder.DropColumn(
                name: "distance_to_route_m",
                table: "ride_requests");

            migrationBuilder.DropColumn(
                name: "suggested_lat",
                table: "ride_requests");

            migrationBuilder.DropColumn(
                name: "suggested_lng",
                table: "ride_requests");
        }
    }
}
