using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReqNest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompletePhaseOneModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ticket_watcher_tickets_ticket_id",
                table: "ticket_watcher");

            migrationBuilder.DropForeignKey(
                name: "fk_ticket_watcher_users_user_id",
                table: "ticket_watcher");

            migrationBuilder.DropPrimaryKey(
                name: "pk_ticket_watcher",
                table: "ticket_watcher");

            migrationBuilder.RenameTable(
                name: "ticket_watcher",
                newName: "ticket_watchers");

            migrationBuilder.RenameIndex(
                name: "ix_ticket_watcher_user_id",
                table: "ticket_watchers",
                newName: "ix_ticket_watchers_user_id");

            migrationBuilder.AddColumn<int>(
                name: "failed_sign_in_count",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_until",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "creation_key",
                table: "tickets",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "first_response_target_at",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "resolution_target_at",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sla_state",
                table: "tickets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "dark_logo_content_type",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logo_content_type",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "first_response_target_minutes",
                table: "projects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "resolution_target_minutes",
                table: "projects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "pk_ticket_watchers",
                table: "ticket_watchers",
                columns: new[] { "ticket_id", "user_id" });

            migrationBuilder.CreateTable(
                name: "account_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<int>(type: "integer", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_account_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comments_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    watcher_updates_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    due_date_updates_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    digest_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_preferences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "report_exports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    filter_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    language = table.Column<int>(type: "integer", nullable: false),
                    time_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    container_name = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: true),
                    blob_name = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_exports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "saved_views",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    filters_json = table.Column<string>(type: "jsonb", nullable: false),
                    sort_json = table.Column<string>(type: "jsonb", nullable: false),
                    columns_json = table.Column<string>(type: "jsonb", nullable: false),
                    group_by = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_views", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_comment_revisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_comment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    edited_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_body = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_comment_revisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_comment_revisions_ticket_comments_ticket_comment_id",
                        column: x => x.ticket_comment_id,
                        principalTable: "ticket_comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_tenant_id_creation_key",
                table: "tickets",
                columns: new[] { "tenant_id", "creation_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_account_tokens_token_hash",
                table: "account_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_account_tokens_user_id_purpose_expires_at",
                table: "account_tokens",
                columns: new[] { "user_id", "purpose", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_preferences_tenant_id_user_id",
                table: "notification_preferences",
                columns: new[] { "tenant_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_report_exports_tenant_id_requested_by_user_id_created_at",
                table: "report_exports",
                columns: new[] { "tenant_id", "requested_by_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_saved_views_tenant_id_owner_user_id_name",
                table: "saved_views",
                columns: new[] { "tenant_id", "owner_user_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_comment_revisions_ticket_comment_id",
                table: "ticket_comment_revisions",
                column: "ticket_comment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_ticket_watchers_tickets_ticket_id",
                table: "ticket_watchers",
                column: "ticket_id",
                principalTable: "tickets",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_ticket_watchers_users_user_id",
                table: "ticket_watchers",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ticket_watchers_tickets_ticket_id",
                table: "ticket_watchers");

            migrationBuilder.DropForeignKey(
                name: "fk_ticket_watchers_users_user_id",
                table: "ticket_watchers");

            migrationBuilder.DropTable(
                name: "account_tokens");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "report_exports");

            migrationBuilder.DropTable(
                name: "saved_views");

            migrationBuilder.DropTable(
                name: "ticket_comment_revisions");

            migrationBuilder.DropIndex(
                name: "ix_tickets_tenant_id_creation_key",
                table: "tickets");

            migrationBuilder.DropPrimaryKey(
                name: "pk_ticket_watchers",
                table: "ticket_watchers");

            migrationBuilder.DropColumn(
                name: "failed_sign_in_count",
                table: "users");

            migrationBuilder.DropColumn(
                name: "locked_until",
                table: "users");

            migrationBuilder.DropColumn(
                name: "creation_key",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "first_response_target_at",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "resolution_target_at",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_state",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "dark_logo_content_type",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "logo_content_type",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "first_response_target_minutes",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "resolution_target_minutes",
                table: "projects");

            migrationBuilder.RenameTable(
                name: "ticket_watchers",
                newName: "ticket_watcher");

            migrationBuilder.RenameIndex(
                name: "ix_ticket_watchers_user_id",
                table: "ticket_watcher",
                newName: "ix_ticket_watcher_user_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_ticket_watcher",
                table: "ticket_watcher",
                columns: new[] { "ticket_id", "user_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_ticket_watcher_tickets_ticket_id",
                table: "ticket_watcher",
                column: "ticket_id",
                principalTable: "tickets",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_ticket_watcher_users_user_id",
                table: "ticket_watcher",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
