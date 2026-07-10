using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReqNest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperationalMaturity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "parent_ticket_id",
                table: "tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "priority_key",
                table: "tickets",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sla_paused_at",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sla_paused_minutes",
                table: "tickets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "sla_policy_id",
                table: "tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sla_policy_name_snapshot",
                table: "tickets",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sla_warning_at",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type_key",
                table: "tickets",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "audit_retention_days",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "deleted_attachment_retention_days",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "notification_retention_days",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "report_retention_days",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "storage_quota_bytes",
                table: "tenants",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "is_published",
                table: "saved_views",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "published_by_user_id",
                table: "saved_views",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "sla_policy_id",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "digest_hour_local",
                table: "notification_preferences",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "email_enabled",
                table: "notification_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_digest_at",
                table: "notification_preferences",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "custom_field_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    label_english = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    label_french = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    options_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_field_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "custom_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    permissions = table.Column<string[]>(type: "text[]", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body_text = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    body_html = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: false),
                    template_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    deduplication_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "report_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    report_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    filter_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    language = table.Column<int>(type: "integer", nullable: false),
                    format = table.Column<int>(type: "integer", nullable: false),
                    frequency = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    next_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_run_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_schedules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sla_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    working_days_mask = table.Column<int>(type: "integer", nullable: false),
                    business_day_start_minutes = table.Column<int>(type: "integer", nullable: false),
                    business_day_end_minutes = table.Column<int>(type: "integer", nullable: false),
                    warning_minutes_before = table.Column<int>(type: "integer", nullable: false),
                    pause_status_keys = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sla_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_custom_field_values",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_custom_field_values", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_priority_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    label_english = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    label_french = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    weight = table.Column<int>(type: "integer", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_priority_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_type_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    label_english = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    label_french = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_type_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "custom_role_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    custom_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    all_projects = table.Column<bool>(type: "boolean", nullable: false),
                    granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_role_grants", x => x.id);
                    table.ForeignKey(
                        name: "fk_custom_role_grants_custom_roles_custom_role_id",
                        column: x => x.custom_role_id,
                        principalTable: "custom_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_custom_role_grants_tenant_memberships_tenant_membership_id",
                        column: x => x.tenant_membership_id,
                        principalTable: "tenant_memberships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sla_holidays",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sla_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sla_holidays", x => x.id);
                    table.ForeignKey(
                        name: "fk_sla_holidays_sla_policies_sla_policy_id",
                        column: x => x.sla_policy_id,
                        principalTable: "sla_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sla_priority_targets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sla_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    priority_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    first_response_minutes = table.Column<int>(type: "integer", nullable: false),
                    resolution_minutes = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sla_priority_targets", x => x.id);
                    table.ForeignKey(
                        name: "fk_sla_priority_targets_sla_policies_sla_policy_id",
                        column: x => x.sla_policy_id,
                        principalTable: "sla_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "custom_role_grant_project",
                columns: table => new
                {
                    custom_role_grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_role_grant_project", x => new { x.custom_role_grant_id, x.project_id });
                    table.ForeignKey(
                        name: "fk_custom_role_grant_project_custom_role_grants_custom_role_gr",
                        column: x => x.custom_role_grant_id,
                        principalTable: "custom_role_grants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_custom_role_grant_project_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_parent_ticket_id",
                table: "tickets",
                column: "parent_ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_custom_field_definitions_tenant_id_project_id_key",
                table: "custom_field_definitions",
                columns: new[] { "tenant_id", "project_id", "key" });

            migrationBuilder.CreateIndex(
                name: "ix_custom_role_grant_project_project_id",
                table: "custom_role_grant_project",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_custom_role_grants_custom_role_id",
                table: "custom_role_grants",
                column: "custom_role_id");

            migrationBuilder.CreateIndex(
                name: "ix_custom_role_grants_tenant_membership_id_custom_role_id",
                table: "custom_role_grants",
                columns: new[] { "tenant_membership_id", "custom_role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_custom_roles_tenant_id_name",
                table: "custom_roles",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_outbox_messages_status_next_attempt_at",
                table: "email_outbox_messages",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_email_outbox_messages_tenant_id_recipient_user_id_deduplica",
                table: "email_outbox_messages",
                columns: new[] { "tenant_id", "recipient_user_id", "deduplication_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_report_schedules_is_active_next_run_at",
                table: "report_schedules",
                columns: new[] { "is_active", "next_run_at" });

            migrationBuilder.CreateIndex(
                name: "ix_report_schedules_tenant_id_owner_user_id_name",
                table: "report_schedules",
                columns: new[] { "tenant_id", "owner_user_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sla_holidays_sla_policy_id_date",
                table: "sla_holidays",
                columns: new[] { "sla_policy_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sla_policies_tenant_id_name",
                table: "sla_policies",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sla_policies_tenant_id_project_id_is_active",
                table: "sla_policies",
                columns: new[] { "tenant_id", "project_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_sla_priority_targets_sla_policy_id_priority_key",
                table: "sla_priority_targets",
                columns: new[] { "sla_policy_id", "priority_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_custom_field_values_ticket_id_definition_id",
                table: "ticket_custom_field_values",
                columns: new[] { "ticket_id", "definition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_priority_definitions_tenant_id_project_id_key",
                table: "ticket_priority_definitions",
                columns: new[] { "tenant_id", "project_id", "key" });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_type_definitions_tenant_id_project_id_key",
                table: "ticket_type_definitions",
                columns: new[] { "tenant_id", "project_id", "key" });

            migrationBuilder.AddForeignKey(
                name: "fk_tickets_tickets_parent_ticket_id",
                table: "tickets",
                column: "parent_ticket_id",
                principalTable: "tickets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tickets_tickets_parent_ticket_id",
                table: "tickets");

            migrationBuilder.DropTable(
                name: "custom_field_definitions");

            migrationBuilder.DropTable(
                name: "custom_role_grant_project");

            migrationBuilder.DropTable(
                name: "email_outbox_messages");

            migrationBuilder.DropTable(
                name: "report_schedules");

            migrationBuilder.DropTable(
                name: "sla_holidays");

            migrationBuilder.DropTable(
                name: "sla_priority_targets");

            migrationBuilder.DropTable(
                name: "ticket_custom_field_values");

            migrationBuilder.DropTable(
                name: "ticket_priority_definitions");

            migrationBuilder.DropTable(
                name: "ticket_type_definitions");

            migrationBuilder.DropTable(
                name: "custom_role_grants");

            migrationBuilder.DropTable(
                name: "sla_policies");

            migrationBuilder.DropTable(
                name: "custom_roles");

            migrationBuilder.DropIndex(
                name: "ix_tickets_parent_ticket_id",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "parent_ticket_id",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "priority_key",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_paused_at",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_paused_minutes",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_policy_id",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_policy_name_snapshot",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_warning_at",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "type_key",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "audit_retention_days",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "deleted_attachment_retention_days",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "notification_retention_days",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "report_retention_days",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "storage_quota_bytes",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "is_published",
                table: "saved_views");

            migrationBuilder.DropColumn(
                name: "published_by_user_id",
                table: "saved_views");

            migrationBuilder.DropColumn(
                name: "sla_policy_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "digest_hour_local",
                table: "notification_preferences");

            migrationBuilder.DropColumn(
                name: "email_enabled",
                table: "notification_preferences");

            migrationBuilder.DropColumn(
                name: "last_digest_at",
                table: "notification_preferences");
        }
    }
}
