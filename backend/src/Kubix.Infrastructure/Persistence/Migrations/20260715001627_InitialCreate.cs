using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kubix.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "universities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_universities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "campuses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    lat = table.Column<double>(type: "double precision", nullable: false),
                    lng = table.Column<double>(type: "double precision", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campuses", x => x.id);
                    table.ForeignKey(
                        name: "FK_campuses_universities_university_id",
                        column: x => x.university_id,
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "university_settings",
                columns: table => new
                {
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allowed_email_domain = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    support_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    max_daily_trips_per_driver = table.Column<int>(type: "integer", nullable: false),
                    min_driver_rating = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    co2_factor_kg_km = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    gamification_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    co2_tracking_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    notify_sos = table.Column<bool>(type: "boolean", nullable: false),
                    notify_block = table.Column<bool>(type: "boolean", nullable: false),
                    notify_weekly_report = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_university_settings", x => x.university_id);
                    table.ForeignKey(
                        name: "FK_university_settings_universities_university_id",
                        column: x => x.university_id,
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "registration_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    campus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    career = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    id_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    vehicle_json = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registration_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_registration_requests_campuses_campus_id",
                        column: x => x.campus_id,
                        principalTable: "campuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_registration_requests_universities_university_id",
                        column: x => x.university_id,
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: true),
                    campus_id = table.Column<Guid>(type: "uuid", nullable: true),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    career = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    id_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    rating_avg = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    eco_balance = table.Column<int>(type: "integer", nullable: false),
                    eco_lifetime = table.Column<int>(type: "integer", nullable: false),
                    must_change_password = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_campuses_campus_id",
                        column: x => x.campus_id,
                        principalTable: "campuses",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_users_universities_university_id",
                        column: x => x.university_id,
                        principalTable: "universities",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    device = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_events_universities_university_id",
                        column: x => x.university_id,
                        principalTable: "universities",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_audit_events_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "eco_token_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    amount = table.Column<int>(type: "integer", nullable: false),
                    source_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eco_token_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_eco_token_transactions_universities_university_id",
                        column: x => x.university_id,
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_eco_token_transactions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "emergency_contacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    relationship = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emergency_contacts", x => x.id);
                    table.ForeignKey(
                        name: "FK_emergency_contacts_universities_university_id",
                        column: x => x.university_id,
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_emergency_contacts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recipient_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    read = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_universities_university_id",
                        column: x => x.university_id,
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notifications_users_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_campus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    origin_lat = table.Column<double>(type: "double precision", nullable: false),
                    origin_lng = table.Column<double>(type: "double precision", nullable: false),
                    departure_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    seats_available = table.Column<int>(type: "integer", nullable: false),
                    polyline = table.Column<string>(type: "text", nullable: true),
                    distance_km = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    co2_saved_kg = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trips", x => x.id);
                    table.ForeignKey(
                        name: "FK_trips_campuses_destination_campus_id",
                        column: x => x.destination_campus_id,
                        principalTable: "campuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_trips_universities_university_id",
                        column: x => x.university_id,
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_trips_users_driver_id",
                        column: x => x.driver_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    make_model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    plate = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    color = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    seats_total = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicles", x => x.id);
                    table.ForeignKey(
                        name: "FK_vehicles_universities_university_id",
                        column: x => x.university_id,
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vehicles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "location_pings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lat = table.Column<double>(type: "double precision", nullable: false),
                    lng = table.Column<double>(type: "double precision", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location_pings", x => x.id);
                    table.ForeignKey(
                        name: "FK_location_pings_trips_trip_id",
                        column: x => x.trip_id,
                        principalTable: "trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_location_pings_universities_university_id",
                        column: x => x.university_id,
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_location_pings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ratings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rater_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rated_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stars = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ratings", x => x.id);
                    table.ForeignKey(
                        name: "FK_ratings_trips_trip_id",
                        column: x => x.trip_id,
                        principalTable: "trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ratings_universities_university_id",
                        column: x => x.university_id,
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ratings_users_rated_id",
                        column: x => x.rated_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ratings_users_rater_id",
                        column: x => x.rater_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ride_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: false),
                    passenger_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pickup_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    pickup_lat = table.Column<double>(type: "double precision", nullable: false),
                    pickup_lng = table.Column<double>(type: "double precision", nullable: false),
                    suggested_lat = table.Column<double>(type: "double precision", nullable: true),
                    suggested_lng = table.Column<double>(type: "double precision", nullable: true),
                    distance_to_route_m = table.Column<double>(type: "double precision", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ride_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_ride_requests_trips_trip_id",
                        column: x => x.trip_id,
                        principalTable: "trips",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ride_requests_universities_university_id",
                        column: x => x.university_id,
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ride_requests_users_passenger_id",
                        column: x => x.passenger_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sos_alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lat = table.Column<double>(type: "double precision", nullable: false),
                    lng = table.Column<double>(type: "double precision", nullable: false),
                    fired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sos_alerts", x => x.id);
                    table.ForeignKey(
                        name: "FK_sos_alerts_trips_trip_id",
                        column: x => x.trip_id,
                        principalTable: "trips",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_sos_alerts_universities_university_id",
                        column: x => x.university_id,
                        principalTable: "universities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sos_alerts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_audit_events_university_id_type_created_at",
                table: "audit_events",
                columns: new[] { "university_id", "type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_user_id",
                table: "audit_events",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_campuses_university_id_name",
                table: "campuses",
                columns: new[] { "university_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eco_token_transactions_university_id",
                table: "eco_token_transactions",
                column: "university_id");

            migrationBuilder.CreateIndex(
                name: "IX_eco_token_transactions_user_id_type_source_id",
                table: "eco_token_transactions",
                columns: new[] { "user_id", "type", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_emergency_contacts_university_id",
                table: "emergency_contacts",
                column: "university_id");

            migrationBuilder.CreateIndex(
                name: "IX_emergency_contacts_user_id",
                table: "emergency_contacts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_location_pings_trip_id_user_id_recorded_at",
                table: "location_pings",
                columns: new[] { "trip_id", "user_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "IX_location_pings_university_id",
                table: "location_pings",
                column: "university_id");

            migrationBuilder.CreateIndex(
                name: "IX_location_pings_user_id",
                table: "location_pings",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_recipient_user_id",
                table: "notifications",
                column: "recipient_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_university_id_read_created_at",
                table: "notifications",
                columns: new[] { "university_id", "read", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ratings_rated_id",
                table: "ratings",
                column: "rated_id");

            migrationBuilder.CreateIndex(
                name: "IX_ratings_rater_id",
                table: "ratings",
                column: "rater_id");

            migrationBuilder.CreateIndex(
                name: "IX_ratings_trip_id_rater_id_rated_id",
                table: "ratings",
                columns: new[] { "trip_id", "rater_id", "rated_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ratings_university_id",
                table: "ratings",
                column: "university_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_registration_requests_campus_id",
                table: "registration_requests",
                column: "campus_id");

            migrationBuilder.CreateIndex(
                name: "IX_registration_requests_university_id",
                table: "registration_requests",
                column: "university_id");

            migrationBuilder.CreateIndex(
                name: "IX_ride_requests_passenger_id",
                table: "ride_requests",
                column: "passenger_id");

            migrationBuilder.CreateIndex(
                name: "IX_ride_requests_trip_id_passenger_id",
                table: "ride_requests",
                columns: new[] { "trip_id", "passenger_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ride_requests_university_id",
                table: "ride_requests",
                column: "university_id");

            migrationBuilder.CreateIndex(
                name: "IX_sos_alerts_trip_id",
                table: "sos_alerts",
                column: "trip_id");

            migrationBuilder.CreateIndex(
                name: "IX_sos_alerts_university_id_status",
                table: "sos_alerts",
                columns: new[] { "university_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_sos_alerts_user_id",
                table: "sos_alerts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_trip_waypoints_trip_id_seq",
                table: "trip_waypoints",
                columns: new[] { "trip_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trip_waypoints_university_id",
                table: "trip_waypoints",
                column: "university_id");

            migrationBuilder.CreateIndex(
                name: "IX_trips_destination_campus_id",
                table: "trips",
                column: "destination_campus_id");

            migrationBuilder.CreateIndex(
                name: "IX_trips_driver_id",
                table: "trips",
                column: "driver_id");

            migrationBuilder.CreateIndex(
                name: "IX_trips_university_id_status_departure_at",
                table: "trips",
                columns: new[] { "university_id", "status", "departure_at" });

            migrationBuilder.CreateIndex(
                name: "IX_universities_slug",
                table: "universities",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_campus_id",
                table: "users",
                column: "campus_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_email_university_id",
                table: "users",
                columns: new[] { "email", "university_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_university_id",
                table: "users",
                column: "university_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_university_id",
                table: "vehicles",
                column: "university_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_user_id",
                table: "vehicles",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "eco_token_transactions");

            migrationBuilder.DropTable(
                name: "emergency_contacts");

            migrationBuilder.DropTable(
                name: "location_pings");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "ratings");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "registration_requests");

            migrationBuilder.DropTable(
                name: "ride_requests");

            migrationBuilder.DropTable(
                name: "sos_alerts");

            migrationBuilder.DropTable(
                name: "trip_waypoints");

            migrationBuilder.DropTable(
                name: "university_settings");

            migrationBuilder.DropTable(
                name: "vehicles");

            migrationBuilder.DropTable(
                name: "trips");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "campuses");

            migrationBuilder.DropTable(
                name: "universities");
        }
    }
}
