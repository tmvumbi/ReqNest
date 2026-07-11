using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReqNest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectSetScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sla_policies_tenant_id_project_id_is_active",
                table: "sla_policies");

            migrationBuilder.DropIndex(
                name: "ix_custom_field_definitions_tenant_id_project_id_key",
                table: "custom_field_definitions");

            migrationBuilder.AddColumn<Guid[]>(
                name: "project_ids",
                table: "sla_policies",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.AddColumn<Guid[]>(
                name: "project_ids",
                table: "custom_field_definitions",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            // Preserve existing single-project scopes as one-element sets.
            migrationBuilder.Sql(
                "UPDATE sla_policies SET project_ids = ARRAY[project_id] WHERE project_id IS NOT NULL;");
            migrationBuilder.Sql(
                "UPDATE custom_field_definitions SET project_ids = ARRAY[project_id] WHERE project_id IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "project_id",
                table: "sla_policies");

            migrationBuilder.DropColumn(
                name: "project_id",
                table: "custom_field_definitions");

            migrationBuilder.CreateIndex(
                name: "ix_sla_policies_tenant_id_is_active",
                table: "sla_policies",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_custom_field_definitions_tenant_id_key",
                table: "custom_field_definitions",
                columns: new[] { "tenant_id", "key" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sla_policies_tenant_id_is_active",
                table: "sla_policies");

            migrationBuilder.DropIndex(
                name: "ix_custom_field_definitions_tenant_id_key",
                table: "custom_field_definitions");

            migrationBuilder.DropColumn(
                name: "project_ids",
                table: "sla_policies");

            migrationBuilder.DropColumn(
                name: "project_ids",
                table: "custom_field_definitions");

            migrationBuilder.AddColumn<Guid>(
                name: "project_id",
                table: "sla_policies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "project_id",
                table: "custom_field_definitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_sla_policies_tenant_id_project_id_is_active",
                table: "sla_policies",
                columns: new[] { "tenant_id", "project_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_custom_field_definitions_tenant_id_project_id_key",
                table: "custom_field_definitions",
                columns: new[] { "tenant_id", "project_id", "key" });
        }
    }
}
