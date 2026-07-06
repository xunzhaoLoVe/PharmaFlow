UPDATE "CollectionSources"
SET "ExportConfig" = REPLACE(
    REPLACE(
    REPLACE(
    REPLACE(
    REPLACE(
    REPLACE(
        "ExportConfig",
        '{{thisMonthStart}} 00:00:00', '{{endDate}} 23:59:59'
    ),
        '{{lastMonthStart}}', '{{startDate}}'
    ),
        '{{lastMonthEnd}}', '{{endDate}}'
    ),
        '{{7daysAgo}}', '{{startDate}}'
    ),
        '{{15daysAgo}}', '{{startDate}}'
    ),
        '{{yesterday}}', '{{endDate}}'
    )
WHERE "IsDeleted" = 0
  AND "ExportConfig" IS NOT NULL;
