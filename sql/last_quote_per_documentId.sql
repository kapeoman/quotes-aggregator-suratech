SELECT DISTINCT ON (q."DocumentId")
       q."Id",
       q."DocumentId",
       q."Amount",
       q."Currency",
       q."Status",
       q."CreatedAt"
FROM "Quotes" AS q
WHERE q."DocumentId" = ANY ($1::text[])
ORDER BY q."DocumentId", q."CreatedAt" DESC, q."Id" DESC;


WITH ranked AS (
    SELECT
        q."Id",
        q."DocumentId",
        q."Amount",
        q."Currency",
        q."Status",
        q."CreatedAt",
        ROW_NUMBER() OVER (
            PARTITION BY q."DocumentId"
            ORDER BY q."CreatedAt" DESC, q."Id" DESC
        ) AS rn
    FROM "Quotes" AS q
    WHERE q."DocumentId" = ANY ($1::text[])
)
SELECT
    "Id",
    "DocumentId",
    "Amount",
    "Currency",
    "Status",
    "CreatedAt"
FROM ranked
WHERE rn = 1;


CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Quotes_DocumentId_CreatedAt_DESC"
ON "Quotes" ("DocumentId", "CreatedAt" DESC)
INCLUDE ("Id", "Amount", "Currency", "Status");


