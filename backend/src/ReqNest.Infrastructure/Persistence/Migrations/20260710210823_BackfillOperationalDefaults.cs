using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReqNest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillOperationalDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE tenants
                SET storage_quota_bytes = CASE WHEN storage_quota_bytes <= 0 THEN 5368709120 ELSE storage_quota_bytes END,
                    notification_retention_days = CASE WHEN notification_retention_days <= 0 THEN 90 ELSE notification_retention_days END,
                    audit_retention_days = CASE WHEN audit_retention_days <= 0 THEN 365 ELSE audit_retention_days END,
                    deleted_attachment_retention_days = CASE WHEN deleted_attachment_retention_days <= 0 THEN 30 ELSE deleted_attachment_retention_days END,
                    report_retention_days = CASE WHEN report_retention_days <= 0 THEN 30 ELSE report_retention_days END;

                UPDATE notification_preferences
                SET digest_hour_local = 8
                WHERE digest_hour_local = 0;

                UPDATE tickets
                SET type_key = CASE type
                        WHEN 0 THEN 'Incident'
                        WHEN 1 THEN 'ServiceRequest'
                        WHEN 2 THEN 'Task'
                        WHEN 3 THEN 'Problem'
                        ELSE 'Incident'
                    END,
                    priority_key = CASE priority
                        WHEN 0 THEN 'Low'
                        WHEN 1 THEN 'Normal'
                        WHEN 2 THEN 'High'
                        WHEN 3 THEN 'Urgent'
                        ELSE 'Normal'
                    END
                WHERE type_key = '' OR priority_key = '';

                INSERT INTO ticket_type_definitions
                    (id, tenant_id, project_id, key, label_english, label_french, "order", is_active, created_at, updated_at)
                SELECT gen_random_uuid(), tenant.id, NULL, definition.key, definition.english, definition.french,
                       definition.sort_order, TRUE, NOW(), NOW()
                FROM tenants tenant
                CROSS JOIN (VALUES
                    ('Incident', 'Incident', 'Incident', 10),
                    ('ServiceRequest', 'Service request', 'Demande de service', 20),
                    ('Task', 'Task', 'Tâche', 30),
                    ('Problem', 'Problem', 'Problème', 40)
                ) AS definition(key, english, french, sort_order)
                WHERE NOT EXISTS (
                    SELECT 1 FROM ticket_type_definitions existing
                    WHERE existing.tenant_id = tenant.id AND existing.project_id IS NULL AND existing.key = definition.key);

                INSERT INTO ticket_priority_definitions
                    (id, tenant_id, project_id, key, label_english, label_french, color, weight, "order", is_active, created_at, updated_at)
                SELECT gen_random_uuid(), tenant.id, NULL, definition.key, definition.english, definition.french,
                       definition.color, definition.weight, definition.sort_order, TRUE, NOW(), NOW()
                FROM tenants tenant
                CROSS JOIN (VALUES
                    ('Low', 'Low', 'Faible', '#64748B', 10, 10),
                    ('Normal', 'Normal', 'Normale', '#2563EB', 40, 20),
                    ('High', 'High', 'Élevée', '#D97706', 70, 30),
                    ('Urgent', 'Urgent', 'Urgente', '#DC2626', 100, 40)
                ) AS definition(key, english, french, color, weight, sort_order)
                WHERE NOT EXISTS (
                    SELECT 1 FROM ticket_priority_definitions existing
                    WHERE existing.tenant_id = tenant.id AND existing.project_id IS NULL AND existing.key = definition.key);

                INSERT INTO sla_policies
                    (id, tenant_id, project_id, name, time_zone, is_default, is_active, working_days_mask,
                     business_day_start_minutes, business_day_end_minutes, warning_minutes_before, pause_status_keys,
                     created_at, updated_at)
                SELECT gen_random_uuid(), tenant.id, NULL, 'Default business hours', tenant.time_zone, TRUE, TRUE, 62,
                       480, 1020, 60, ARRAY['WAITING', 'ON_HOLD']::text[], NOW(), NOW()
                FROM tenants tenant
                WHERE NOT EXISTS (SELECT 1 FROM sla_policies policy WHERE policy.tenant_id = tenant.id);

                INSERT INTO sla_priority_targets
                    (id, tenant_id, sla_policy_id, priority_key, first_response_minutes, resolution_minutes, created_at, updated_at)
                SELECT gen_random_uuid(), policy.tenant_id, policy.id, target.priority_key,
                       target.first_response_minutes, target.resolution_minutes, NOW(), NOW()
                FROM sla_policies policy
                CROSS JOIN (VALUES
                    ('Low', 480, 2880),
                    ('Normal', 240, 1440),
                    ('High', 60, 480),
                    ('Urgent', 30, 240)
                ) AS target(priority_key, first_response_minutes, resolution_minutes)
                WHERE policy.name = 'Default business hours'
                  AND NOT EXISTS (
                      SELECT 1 FROM sla_priority_targets existing
                      WHERE existing.sla_policy_id = policy.id AND existing.priority_key = target.priority_key);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
