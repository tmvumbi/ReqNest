using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReqNest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExternalServiceIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tickets_users_reporter_user_id",
                table: "tickets");

            migrationBuilder.AlterColumn<Guid>(
                name: "reporter_user_id",
                table: "tickets",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "reporter_display_name_snapshot",
                table: "tickets",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "reporter_email_snapshot",
                table: "tickets",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "requester_identity_id",
                table: "tickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE tickets AS ticket
                SET reporter_email_snapshot = account.email,
                    reporter_display_name_snapshot = account.display_name
                FROM users AS account
                WHERE ticket.reporter_user_id = account.id;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "changed_by_user_id",
                table: "ticket_status_history",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "requester_portal_enabled",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "requester_portal_introduction_english",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "requester_portal_introduction_french",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "requester_portal_enabled",
                table: "projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<Guid>(
                name: "uploaded_by_user_id",
                table: "attachments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "requester_identity_id",
                table: "attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_assistance_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    input_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    draft_output = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    evaluation_score = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_assistance_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_ai_assistance_requests_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ai_assistance_requests_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_tenant_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    provider = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    protected_credential = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    allowed_kinds = table.Column<int[]>(type: "integer[]", nullable: false),
                    require_human_review = table.Column<bool>(type: "boolean", nullable: false),
                    allow_attachment_content = table.Column<bool>(type: "boolean", nullable: false),
                    non_training_assurance_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    evaluation_version = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_tenant_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "api_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    prefix = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    scopes = table.Column<string[]>(type: "text[]", nullable: false),
                    project_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_api_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_api_tokens_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_identity_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    email_snapshot = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_identity_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_external_identity_links_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inbound_email_channels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    address = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    secret_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    default_type_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    default_priority_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbound_email_channels", x => x.id);
                    table.ForeignKey(
                        name: "fk_inbound_email_channels_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "integration_connections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    protected_configuration = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    last_checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    retry_attempts = table.Column<int>(type: "integer", nullable: false),
                    next_retry_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integration_connections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_articles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    slug = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    title_english = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    title_french = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body_english = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    body_french = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    search_text = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    visibility = table.Column<int>(type: "integer", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_knowledge_articles", x => x.id);
                    table.ForeignKey(
                        name: "fk_knowledge_articles_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_knowledge_articles_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "requester_identities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    display_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    preferred_language = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_requester_identities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sso_authentication_exchanges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sso_authentication_exchanges", x => x.id);
                    table.ForeignKey(
                        name: "fk_sso_authentication_exchanges_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_sso_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    authority = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    client_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    protected_client_secret = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    allowed_email_domains = table.Column<string[]>(type: "text[]", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    require_sso = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_sso_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    protected_secret = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    event_types = table.Column<string[]>(type: "text[]", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webhook_subscriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbound_email_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: true),
                    message_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    in_reply_to = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sender_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    failure_code = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbound_email_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_inbound_email_messages_inbound_email_channels_channel_id",
                        column: x => x.channel_id,
                        principalTable: "inbound_email_channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_inbound_email_messages_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ticket_knowledge_articles",
                columns: table => new
                {
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    knowledge_article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_knowledge_articles", x => new { x.ticket_id, x.knowledge_article_id });
                    table.ForeignKey(
                        name: "fk_ticket_knowledge_articles_knowledge_articles_knowledge_arti",
                        column: x => x.knowledge_article_id,
                        principalTable: "knowledge_articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ticket_knowledge_articles_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ticket_knowledge_articles_users_linked_by_user_id",
                        column: x => x.linked_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "requester_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_identity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: false),
                    body_plain_text = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_requester_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_requester_comments_requester_identities_requester_identity_",
                        column: x => x.requester_identity_id,
                        principalTable: "requester_identities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_requester_comments_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "requester_ticket_accesses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_identity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_accessed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_requester_ticket_accesses", x => x.id);
                    table.ForeignKey(
                        name: "fk_requester_ticket_accesses_requester_identities_requester_id",
                        column: x => x.requester_identity_id,
                        principalTable: "requester_identities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_requester_ticket_accesses_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "webhook_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    event_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_status_code = table.Column<int>(type: "integer", nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webhook_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "fk_webhook_deliveries_webhook_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "webhook_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_requester_identity_id",
                table: "tickets",
                column: "requester_identity_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_requester_identity_id",
                table: "attachments",
                column: "requester_identity_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_assistance_requests_requested_by_user_id",
                table: "ai_assistance_requests",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_assistance_requests_tenant_id_ticket_id_created_at",
                table: "ai_assistance_requests",
                columns: new[] { "tenant_id", "ticket_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_assistance_requests_ticket_id",
                table: "ai_assistance_requests",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_tenant_configurations_tenant_id",
                table: "ai_tenant_configurations",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_api_tokens_created_by_user_id",
                table: "api_tokens",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_api_tokens_tenant_id_prefix",
                table: "api_tokens",
                columns: new[] { "tenant_id", "prefix" });

            migrationBuilder.CreateIndex(
                name: "ix_api_tokens_token_hash",
                table: "api_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_external_identity_links_tenant_id_provider_subject",
                table: "external_identity_links",
                columns: new[] { "tenant_id", "provider", "subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_external_identity_links_tenant_id_user_id_provider",
                table: "external_identity_links",
                columns: new[] { "tenant_id", "user_id", "provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_external_identity_links_user_id",
                table: "external_identity_links",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_inbound_email_channels_project_id",
                table: "inbound_email_channels",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_inbound_email_channels_tenant_id_address",
                table: "inbound_email_channels",
                columns: new[] { "tenant_id", "address" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inbound_email_messages_channel_id_message_id",
                table: "inbound_email_messages",
                columns: new[] { "channel_id", "message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inbound_email_messages_ticket_id",
                table: "inbound_email_messages",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_integration_connections_tenant_id_provider_name",
                table: "integration_connections",
                columns: new[] { "tenant_id", "provider", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_articles_author_user_id",
                table: "knowledge_articles",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_articles_project_id",
                table: "knowledge_articles",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_articles_tenant_id_slug",
                table: "knowledge_articles",
                columns: new[] { "tenant_id", "slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_articles_tenant_id_status_visibility",
                table: "knowledge_articles",
                columns: new[] { "tenant_id", "status", "visibility" });

            migrationBuilder.CreateIndex(
                name: "ix_requester_comments_requester_identity_id",
                table: "requester_comments",
                column: "requester_identity_id");

            migrationBuilder.CreateIndex(
                name: "ix_requester_comments_ticket_id_created_at",
                table: "requester_comments",
                columns: new[] { "ticket_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_requester_identities_tenant_id_normalized_email",
                table: "requester_identities",
                columns: new[] { "tenant_id", "normalized_email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_requester_ticket_accesses_requester_identity_id_ticket_id",
                table: "requester_ticket_accesses",
                columns: new[] { "requester_identity_id", "ticket_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_requester_ticket_accesses_ticket_id",
                table: "requester_ticket_accesses",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_requester_ticket_accesses_token_hash",
                table: "requester_ticket_accesses",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sso_authentication_exchanges_code_hash",
                table: "sso_authentication_exchanges",
                column: "code_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sso_authentication_exchanges_expires_at",
                table: "sso_authentication_exchanges",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_sso_authentication_exchanges_user_id",
                table: "sso_authentication_exchanges",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_sso_configurations_tenant_id",
                table: "tenant_sso_configurations",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_knowledge_articles_knowledge_article_id",
                table: "ticket_knowledge_articles",
                column: "knowledge_article_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_knowledge_articles_linked_by_user_id",
                table: "ticket_knowledge_articles",
                column: "linked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_webhook_deliveries_status_next_attempt_at",
                table: "webhook_deliveries",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_webhook_deliveries_subscription_id_event_key",
                table: "webhook_deliveries",
                columns: new[] { "subscription_id", "event_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_webhook_subscriptions_tenant_id_name",
                table: "webhook_subscriptions",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_attachments_requester_identities_requester_identity_id",
                table: "attachments",
                column: "requester_identity_id",
                principalTable: "requester_identities",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tickets_requester_identities_requester_identity_id",
                table: "tickets",
                column: "requester_identity_id",
                principalTable: "requester_identities",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_tickets_users_reporter_user_id",
                table: "tickets",
                column: "reporter_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_attachments_requester_identities_requester_identity_id",
                table: "attachments");

            migrationBuilder.DropForeignKey(
                name: "fk_tickets_requester_identities_requester_identity_id",
                table: "tickets");

            migrationBuilder.DropForeignKey(
                name: "fk_tickets_users_reporter_user_id",
                table: "tickets");

            migrationBuilder.Sql("DELETE FROM tickets WHERE reporter_user_id IS NULL;");

            migrationBuilder.DropTable(
                name: "ai_assistance_requests");

            migrationBuilder.DropTable(
                name: "ai_tenant_configurations");

            migrationBuilder.DropTable(
                name: "api_tokens");

            migrationBuilder.DropTable(
                name: "external_identity_links");

            migrationBuilder.DropTable(
                name: "inbound_email_messages");

            migrationBuilder.DropTable(
                name: "integration_connections");

            migrationBuilder.DropTable(
                name: "requester_comments");

            migrationBuilder.DropTable(
                name: "requester_ticket_accesses");

            migrationBuilder.DropTable(
                name: "sso_authentication_exchanges");

            migrationBuilder.DropTable(
                name: "tenant_sso_configurations");

            migrationBuilder.DropTable(
                name: "ticket_knowledge_articles");

            migrationBuilder.DropTable(
                name: "webhook_deliveries");

            migrationBuilder.DropTable(
                name: "inbound_email_channels");

            migrationBuilder.DropTable(
                name: "requester_identities");

            migrationBuilder.DropTable(
                name: "knowledge_articles");

            migrationBuilder.DropTable(
                name: "webhook_subscriptions");

            migrationBuilder.DropIndex(
                name: "ix_tickets_requester_identity_id",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_attachments_requester_identity_id",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "reporter_display_name_snapshot",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "reporter_email_snapshot",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "requester_identity_id",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "requester_portal_enabled",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "requester_portal_introduction_english",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "requester_portal_introduction_french",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "requester_portal_enabled",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "requester_identity_id",
                table: "attachments");

            migrationBuilder.AlterColumn<Guid>(
                name: "reporter_user_id",
                table: "tickets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "changed_by_user_id",
                table: "ticket_status_history",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "uploaded_by_user_id",
                table: "attachments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_tickets_users_reporter_user_id",
                table: "tickets",
                column: "reporter_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
