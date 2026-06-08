using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SS.PaymentService.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inbox_events",
                columns: table => new
                {
                    message_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    event_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "PROCESSED"),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_events", x => x.message_id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    event_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    aggregate_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "PENDING"),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    order_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    user_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "IDR"),
                    payment_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Midtrans"),
                    payment_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    payment_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    snap_token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    snap_redirect_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "System"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payment_refunds",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    payment_id = table.Column<int>(type: "integer", nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    refund_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    refund_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    refund_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "System"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_refunds", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_refunds_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_outbox_events_status_created",
                table: "outbox_events",
                columns: new[] { "status", "created_at" },
                filter: "status = 'PENDING'");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_events_public_id",
                table: "outbox_events",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_refunds_payment_id",
                table: "payment_refunds",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_refunds_public_id",
                table: "payment_refunds",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_order_public_id",
                table: "payments",
                column: "order_public_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_public_id",
                table: "payments",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox_events");

            migrationBuilder.DropTable(
                name: "outbox_events");

            migrationBuilder.DropTable(
                name: "payment_refunds");

            migrationBuilder.DropTable(
                name: "payments");
        }
    }
}
