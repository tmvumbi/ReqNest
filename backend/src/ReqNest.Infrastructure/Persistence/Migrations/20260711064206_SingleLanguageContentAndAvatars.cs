using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReqNest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SingleLanguageContentAndAvatars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "name_french",
                table: "workflow_transitions");

            migrationBuilder.DropColumn(
                name: "label_french",
                table: "workflow_statuses");

            migrationBuilder.DropColumn(
                name: "label_french",
                table: "ticket_type_definitions");

            migrationBuilder.DropColumn(
                name: "label_french",
                table: "ticket_priority_definitions");

            migrationBuilder.DropColumn(
                name: "requester_portal_introduction_french",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "name_french",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "summary_french",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "body_french",
                table: "knowledge_articles");

            migrationBuilder.DropColumn(
                name: "title_french",
                table: "knowledge_articles");

            migrationBuilder.DropColumn(
                name: "label_french",
                table: "custom_field_definitions");

            migrationBuilder.RenameColumn(
                name: "name_english",
                table: "workflow_transitions",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "label_english",
                table: "workflow_statuses",
                newName: "label");

            migrationBuilder.RenameColumn(
                name: "label_english",
                table: "ticket_type_definitions",
                newName: "label");

            migrationBuilder.RenameColumn(
                name: "label_english",
                table: "ticket_priority_definitions",
                newName: "label");

            migrationBuilder.RenameColumn(
                name: "requester_portal_introduction_english",
                table: "tenants",
                newName: "requester_portal_introduction");

            migrationBuilder.RenameColumn(
                name: "name_english",
                table: "projects",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "summary_english",
                table: "notifications",
                newName: "summary");

            migrationBuilder.RenameColumn(
                name: "title_english",
                table: "knowledge_articles",
                newName: "title");

            migrationBuilder.RenameColumn(
                name: "body_english",
                table: "knowledge_articles",
                newName: "body");

            migrationBuilder.RenameColumn(
                name: "label_english",
                table: "custom_field_definitions",
                newName: "label");

            migrationBuilder.AddColumn<string>(
                name: "avatar_blob_name",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "avatar_content_type",
                table: "users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "avatar_blob_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "avatar_content_type",
                table: "users");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "workflow_transitions",
                newName: "name_english");

            migrationBuilder.RenameColumn(
                name: "label",
                table: "workflow_statuses",
                newName: "label_english");

            migrationBuilder.RenameColumn(
                name: "label",
                table: "ticket_type_definitions",
                newName: "label_english");

            migrationBuilder.RenameColumn(
                name: "label",
                table: "ticket_priority_definitions",
                newName: "label_english");

            migrationBuilder.RenameColumn(
                name: "requester_portal_introduction",
                table: "tenants",
                newName: "requester_portal_introduction_english");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "projects",
                newName: "name_english");

            migrationBuilder.RenameColumn(
                name: "summary",
                table: "notifications",
                newName: "summary_english");

            migrationBuilder.RenameColumn(
                name: "title",
                table: "knowledge_articles",
                newName: "title_english");

            migrationBuilder.RenameColumn(
                name: "body",
                table: "knowledge_articles",
                newName: "body_english");

            migrationBuilder.RenameColumn(
                name: "label",
                table: "custom_field_definitions",
                newName: "label_english");

            migrationBuilder.AddColumn<string>(
                name: "name_french",
                table: "workflow_transitions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "label_french",
                table: "workflow_statuses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "label_french",
                table: "ticket_type_definitions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "label_french",
                table: "ticket_priority_definitions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "requester_portal_introduction_french",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name_french",
                table: "projects",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "summary_french",
                table: "notifications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "body_french",
                table: "knowledge_articles",
                type: "character varying(100000)",
                maxLength: 100000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "title_french",
                table: "knowledge_articles",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "label_french",
                table: "custom_field_definitions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");
        }
    }
}
