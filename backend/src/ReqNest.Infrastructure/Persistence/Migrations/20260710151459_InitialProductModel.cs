using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReqNest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialProductModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    target_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    target_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    short_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    default_language = table.Column<int>(type: "integer", nullable: false),
                    time_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    default_theme = table.Column<int>(type: "integer", nullable: false),
                    primary_color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    logo_blob_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    dark_logo_blob_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    support_contact = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    report_footer_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_relationships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_relationships", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    display_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    preferred_language = table.Column<int>(type: "integer", nullable: false),
                    theme_preference = table.Column<int>(type: "integer", nullable: false),
                    last_signed_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    summary_english = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    summary_french = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    deep_link = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    group_key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_users_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    invitation_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    invitation_token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_memberships", x => x.id);
                    table.ForeignKey(
                        name: "fk_tenant_memberships_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_memberships_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    all_projects = table.Column<bool>(type: "boolean", nullable: false),
                    granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_grants", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_grants_tenant_memberships_tenant_membership_id",
                        column: x => x.tenant_membership_id,
                        principalTable: "tenant_memberships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    container_name = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    blob_name = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    checksum_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    scan_status = table.Column<int>(type: "integer", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    name_english = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_french = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    next_ticket_number = table.Column<long>(type: "bigint", nullable: false),
                    default_assignee_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    default_priority = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projects", x => x.id);
                    table.ForeignKey(
                        name: "fk_projects_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_grant_project",
                columns: table => new
                {
                    role_grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_grant_project", x => new { x.role_grant_id, x.project_id });
                    table.ForeignKey(
                        name: "fk_role_grant_project_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_role_grant_project_role_grants_role_grant_id",
                        column: x => x.role_grant_id,
                        principalTable: "role_grants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflows", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflows_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_workflows_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_statuses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label_english = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    label_french = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_initial = table.Column<bool>(type: "boolean", nullable: false),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_statuses", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_statuses_workflows_workflow_id",
                        column: x => x.workflow_id,
                        principalTable: "workflows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tickets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<long>(type: "bigint", nullable: false),
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    description_plain_text = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    workflow_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignee_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    labels = table.Column<string[]>(type: "text[]", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    first_responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolution_summary = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tickets", x => x.id);
                    table.ForeignKey(
                        name: "fk_tickets_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tickets_users_assignee_user_id",
                        column: x => x.assignee_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tickets_users_reporter_user_id",
                        column: x => x.reporter_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tickets_workflow_statuses_workflow_status_id",
                        column: x => x.workflow_status_id,
                        principalTable: "workflow_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_transitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_english = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    name_french = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    comment_required = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_transitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_transitions_workflow_statuses_from_status_id",
                        column: x => x.from_status_id,
                        principalTable: "workflow_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_workflow_transitions_workflow_statuses_to_status_id",
                        column: x => x.to_status_id,
                        principalTable: "workflow_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_workflow_transitions_workflows_workflow_id",
                        column: x => x.workflow_id,
                        principalTable: "workflows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ticket_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: false),
                    body_plain_text = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_comments_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ticket_comments_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ticket_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_status_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_status_history_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ticket_watcher",
                columns: table => new
                {
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_muted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_watcher", x => new { x.ticket_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_ticket_watcher_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ticket_watcher_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attachments_tenant_id_blob_name",
                table: "attachments",
                columns: new[] { "tenant_id", "blob_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attachments_ticket_comment_id",
                table: "attachments",
                column: "ticket_comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_ticket_id",
                table: "attachments",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_tenant_id_created_at",
                table: "audit_events",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_recipient_user_id_event_key",
                table: "notifications",
                columns: new[] { "recipient_user_id", "event_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_tenant_id_recipient_user_id_read_at",
                table: "notifications",
                columns: new[] { "tenant_id", "recipient_user_id", "read_at" });

            migrationBuilder.CreateIndex(
                name: "ix_projects_tenant_id_key",
                table: "projects",
                columns: new[] { "tenant_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_workflow_id",
                table: "projects",
                column: "workflow_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_grant_project_project_id",
                table: "role_grant_project",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_grants_tenant_membership_id_role_all_projects",
                table: "role_grants",
                columns: new[] { "tenant_membership_id", "role", "all_projects" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_memberships_tenant_id_user_id",
                table: "tenant_memberships",
                columns: new[] { "tenant_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_memberships_user_id",
                table: "tenant_memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_comments_author_user_id",
                table: "ticket_comments",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_comments_ticket_id_created_at",
                table: "ticket_comments",
                columns: new[] { "ticket_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_relationships_source_ticket_id_target_ticket_id_type",
                table: "ticket_relationships",
                columns: new[] { "source_ticket_id", "target_ticket_id", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_status_history_ticket_id_created_at",
                table: "ticket_status_history",
                columns: new[] { "ticket_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_watcher_user_id",
                table: "ticket_watcher",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_assignee_user_id",
                table: "tickets",
                column: "assignee_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_project_id_number",
                table: "tickets",
                columns: new[] { "project_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tickets_reporter_user_id",
                table: "tickets",
                column: "reporter_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_tenant_id_assignee_user_id_is_archived",
                table: "tickets",
                columns: new[] { "tenant_id", "assignee_user_id", "is_archived" });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_tenant_id_key",
                table: "tickets",
                columns: new[] { "tenant_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tickets_tenant_id_project_id_workflow_status_id",
                table: "tickets",
                columns: new[] { "tenant_id", "project_id", "workflow_status_id" });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_workflow_status_id",
                table: "tickets",
                column: "workflow_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_token_hash",
                table: "user_sessions",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_user_id_expires_at",
                table: "user_sessions",
                columns: new[] { "user_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_users_normalized_email",
                table: "users",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_statuses_workflow_id_key",
                table: "workflow_statuses",
                columns: new[] { "workflow_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_statuses_workflow_id_order",
                table: "workflow_statuses",
                columns: new[] { "workflow_id", "order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_transitions_from_status_id",
                table: "workflow_transitions",
                column: "from_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_transitions_to_status_id",
                table: "workflow_transitions",
                column: "to_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_transitions_workflow_id_from_status_id_to_status_id",
                table: "workflow_transitions",
                columns: new[] { "workflow_id", "from_status_id", "to_status_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflows_project_id",
                table: "workflows",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflows_tenant_id_name",
                table: "workflows",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_attachments_ticket_comments_ticket_comment_id",
                table: "attachments",
                column: "ticket_comment_id",
                principalTable: "ticket_comments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_attachments_tickets_ticket_id",
                table: "attachments",
                column: "ticket_id",
                principalTable: "tickets",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_projects_workflows_workflow_id",
                table: "projects",
                column: "workflow_id",
                principalTable: "workflows",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_projects_tenants_tenant_id",
                table: "projects");

            migrationBuilder.DropForeignKey(
                name: "fk_workflows_tenants_tenant_id",
                table: "workflows");

            migrationBuilder.DropForeignKey(
                name: "fk_projects_workflows_workflow_id",
                table: "projects");

            migrationBuilder.DropTable(
                name: "attachments");

            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "role_grant_project");

            migrationBuilder.DropTable(
                name: "ticket_relationships");

            migrationBuilder.DropTable(
                name: "ticket_status_history");

            migrationBuilder.DropTable(
                name: "ticket_watcher");

            migrationBuilder.DropTable(
                name: "user_sessions");

            migrationBuilder.DropTable(
                name: "workflow_transitions");

            migrationBuilder.DropTable(
                name: "ticket_comments");

            migrationBuilder.DropTable(
                name: "role_grants");

            migrationBuilder.DropTable(
                name: "tickets");

            migrationBuilder.DropTable(
                name: "tenant_memberships");

            migrationBuilder.DropTable(
                name: "workflow_statuses");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropTable(
                name: "workflows");

            migrationBuilder.DropTable(
                name: "projects");
        }
    }
}
