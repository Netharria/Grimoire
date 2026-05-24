# Spec: Migration Plan

> **Status: Implemented** — Single migration `20260524033203_ModelRedesign` generated and verified. Key corrections applied: XpHistory.Type conversion uses raw SQL `ALTER COLUMN TYPE USING CASE` (PostgreSQL requires explicit USING for int→varchar); old XpHistory FKs/indexes manually dropped (they existed in DB but were absent from the model snapshot); ModerationActor.System actor uses `NULL` in the `ModeratorId` column so both Sins and SinReasonHistory keep that column nullable (`.IsRequired(false)` added to both EF configurations); all data migration SQL (SinReasonHistory INSERT, Pardons data copy, CustomCommands/Role backfill, MessageHistory discriminator backfill, ProxiedMessages NULL cleanup) runs before the corresponding DDL drops.

All schema changes from the current feature branch collected into a single EF Core migration.
Steps are ordered by dependency. Each step notes which spec it comes from.

Do not generate the EF migration until all domain model and EF configuration code changes are
complete across all specs.

---

## Step 1 — `SinReasonHistory` table

**Source:** existing pending work (not a new spec)
**Depends on:** `Sins` table

**Up:**
```sql
CREATE TABLE "SinReasonHistory" (
    "SinId"       bigint        NOT NULL,
    "SetAt"       timestamptz   NOT NULL,
    "Reason"      varchar(1000) NOT NULL,
    "ModeratorId" bigint        NULL,
    CONSTRAINT "PK_SinReasonHistory" PRIMARY KEY ("SinId", "SetAt"),
    CONSTRAINT "FK_SinReasonHistory_Sins_SinId"
        FOREIGN KEY ("SinId") REFERENCES "Sins" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_SinReasonHistory_SinId_SetAt"
    ON "SinReasonHistory" ("SinId" ASC, "SetAt" DESC);

-- Migrate existing non-empty reasons from Sins.
-- Uses SinOn as the initial SetAt (best available timestamp for when the reason was first set).
-- trim() is used so whitespace-only reasons are skipped.
INSERT INTO "SinReasonHistory" ("SinId", "SetAt", "Reason", "ModeratorId")
SELECT "Id", "SinOn", "Reason", "ModeratorId"
FROM "Sins"
WHERE trim("Reason") <> '';

ALTER TABLE "Sins" DROP COLUMN "Reason";
```

**Down:**
```sql
ALTER TABLE "Sins" ADD COLUMN "Reason" varchar(1000) NOT NULL DEFAULT '';

UPDATE "Sins" s
SET "Reason" = r."Reason"
FROM (
    SELECT DISTINCT ON ("SinId") "SinId", "Reason"
    FROM "SinReasonHistory"
    ORDER BY "SinId", "SetAt" DESC
) r
WHERE s."Id" = r."SinId";

DROP TABLE "SinReasonHistory";
```

---

## Step 2 — `SinType` enum: add `Kick`

**Source:** existing pending work
**Depends on:** nothing

No SQL required. `SinType` is stored as `int`. `Kick = 3` is a new value that does not conflict
with existing rows (`Warn = 0`, `Mute = 1`, `Ban = 2`). EF handles this via the C# enum.

---

## Step 3 — `PublishType` enum: add `Kick`

**Source:** existing pending work
**Depends on:** nothing

No SQL required. `PublishType` is stored as `int`. `Kick = 2` (`Ban = 0`, `Unban = 1`, `Kick = 2`).
No existing rows use value 2.

---

## Step 4 — `Pardons` table: composite PK + add `SetAt`

**Source:** existing pending work
**Depends on:** nothing

> **Note:** EF Core generates an ADD COLUMN + DROP COLUMN pair rather than RENAME COLUMN. The
> explicit `UPDATE` below must run before `PardonDate` is dropped, otherwise all existing pardon
> dates are lost. The `migrationBuilder.Sql(...)` call must appear between the `AddColumn` and
> `DropColumn` calls in the C# migration.

**Up:**
```sql
ALTER TABLE "Pardons" ADD COLUMN "SetAt" timestamptz NOT NULL DEFAULT '0001-01-01T00:00:00Z';

-- Copy existing dates before the old column is dropped.
UPDATE "Pardons" SET "SetAt" = "PardonDate";

ALTER TABLE "Pardons" DROP CONSTRAINT "PK_Pardons";
ALTER TABLE "Pardons" ADD CONSTRAINT "PK_Pardons" PRIMARY KEY ("SinId", "SetAt");

CREATE INDEX "IX_Pardons_SinId_SetAt" ON "Pardons" ("SinId" ASC, "SetAt" DESC);

ALTER TABLE "Pardons" DROP COLUMN "PardonDate";
```

**Down:**
```sql
DROP INDEX "IX_Pardons_SinId_SetAt";

ALTER TABLE "Pardons" DROP CONSTRAINT "PK_Pardons";

-- Remove duplicates before restoring the single-column PK, keeping the earliest row per sin.
DELETE FROM "Pardons" p
WHERE EXISTS (
    SELECT 1 FROM "Pardons" p2
    WHERE p2."SinId" = p."SinId" AND p2."SetAt" < p."SetAt"
);

ALTER TABLE "Pardons" ADD COLUMN "PardonDate" timestamptz NOT NULL DEFAULT now();
UPDATE "Pardons" SET "PardonDate" = "SetAt";

ALTER TABLE "Pardons" ADD CONSTRAINT "PK_Pardons" PRIMARY KEY ("SinId");
ALTER TABLE "Pardons" DROP COLUMN "SetAt";
```

---

## Step 5 — `Trackers` table: drop

**Source:** existing pending work
**Depends on:** nothing

The `Trackers` table is being removed. The `FK_Mutes_Sins_SinId` foreign key (which used
`RESTRICT` delete behaviour) is also dropped in this step, since EF no longer maps a navigation
from `Mutes` back to `Sins` through a tracked-user scope.

**Up:**
```sql
ALTER TABLE "Mutes" DROP CONSTRAINT "FK_Mutes_Sins_SinId";
DROP TABLE "Trackers";
```

**Down:**
```sql
CREATE TABLE "Trackers" (
    "UserId"       numeric(20,0) NOT NULL,
    "GuildId"      numeric(20,0) NOT NULL,
    "EndTime"      timestamptz   NOT NULL,
    "LogChannelId" numeric(20,0) NOT NULL,
    "ModeratorId"  numeric(20,0) NULL,
    CONSTRAINT "PK_Trackers" PRIMARY KEY ("UserId", "GuildId")
);
CREATE INDEX "IX_Trackers_EndTime" ON "Trackers" ("EndTime");

ALTER TABLE "Mutes"
    ADD CONSTRAINT "FK_Mutes_Sins_SinId"
        FOREIGN KEY ("SinId") REFERENCES "Sins" ("Id") ON DELETE RESTRICT;
```

---

## Step 6 — `CustomCommands`: TPH discriminator, versioning, usage tracking

**Source:** existing pending work + [`spec-custom-command-role-policy.md`](spec-custom-command-role-policy.md)
**Depends on:** nothing

This step restructures `CustomCommands` and `CustomCommandsRole` to support:
- TPH inheritance (`TextCustomCommand` / `EmbedCustomCommand` via `CommandType` discriminator)
- TPH inheritance (`CustomCommandAllowRole` / `CustomCommandDenyRole` via `RoleType` discriminator)
- Version history (composite PK with `CreatedAt`)
- Usage tracking (`CustomCommandUsages` table)

**Up:**
```sql
-- 1. Add new CustomCommands columns with safe defaults.
ALTER TABLE "CustomCommands"
    ADD COLUMN "CreatedAt"   timestamptz   NOT NULL DEFAULT '0001-01-01T00:00:00Z',
    ADD COLUMN "ModeratorId" numeric(20,0) NULL,
    ADD COLUMN "CommandType" varchar(13)   NOT NULL DEFAULT '';

-- 2. Backfill CreatedAt and CommandType from the old IsEmbedded boolean flag.
--    Must run before IsEmbedded is dropped.
UPDATE "CustomCommands"
SET "CreatedAt"   = NOW(),
    "CommandType" = CASE WHEN "IsEmbedded" THEN 'Embed' ELSE 'Text' END;

-- 3. Add new CustomCommandsRole columns.
ALTER TABLE "CustomCommandsRole"
    ADD COLUMN "Name"     varchar(24)  NOT NULL DEFAULT '',
    ADD COLUMN "CreatedAt" timestamptz NOT NULL DEFAULT '0001-01-01T00:00:00Z',
    ADD COLUMN "RoleType"  varchar(21)  NOT NULL DEFAULT '';

-- 4. Backfill the new role columns.
--    All legacy roles were allow-type; RoleType = 'Allow' for all existing rows.
--    Must run after step 2 (so CustomCommands.CreatedAt is set) and before
--    CustomCommandName is dropped.
UPDATE "CustomCommandsRole" AS ccr
SET "Name"      = ccr."CustomCommandName",
    "CreatedAt" = cc."CreatedAt",
    "RoleType"  = 'Allow'
FROM "CustomCommands" AS cc
WHERE cc."Name"    = ccr."CustomCommandName"
  AND cc."GuildId" = ccr."GuildId";

-- 5. Swap PKs and rebuild the FK between the two tables.
ALTER TABLE "CustomCommandsRole"
    DROP CONSTRAINT "FK_CustomCommandsRole_CustomCommands_CustomCommandName_GuildId";

ALTER TABLE "CustomCommandsRole" DROP CONSTRAINT "PK_CustomCommandsRole";
ALTER TABLE "CustomCommandsRole"
    ADD CONSTRAINT "PK_CustomCommandsRole" PRIMARY KEY ("Name", "GuildId", "CreatedAt", "RoleId");

ALTER TABLE "CustomCommands" DROP CONSTRAINT "PK_CustomCommands";
ALTER TABLE "CustomCommands"
    ADD CONSTRAINT "PK_CustomCommands" PRIMARY KEY ("Name", "GuildId", "CreatedAt");

ALTER TABLE "CustomCommandsRole"
    ADD CONSTRAINT "FK_CustomCommandsRole_CustomCommands_Name_GuildId_CreatedAt"
        FOREIGN KEY ("Name", "GuildId", "CreatedAt")
        REFERENCES "CustomCommands" ("Name", "GuildId", "CreatedAt") ON DELETE CASCADE;

-- 6. Drop old columns now that the data has been migrated.
ALTER TABLE "CustomCommandsRole"
    DROP COLUMN "CustomCommandName";

ALTER TABLE "CustomCommands"
    DROP COLUMN "HasMention",
    DROP COLUMN "HasMessage",
    DROP COLUMN "IsEmbedded",
    DROP COLUMN "RestrictedUse";

-- 7. Index for autocomplete queries.
CREATE INDEX "IX_CustomCommands_GuildId_Name" ON "CustomCommands" ("GuildId", "Name");

-- 8. Usage tracking table.
CREATE TABLE "CustomCommandUsages" (
    "Name"    varchar(24)   NOT NULL,
    "GuildId" numeric(20,0) NOT NULL,
    "UserId"  numeric(20,0) NOT NULL,
    "UsedAt"  timestamptz   NOT NULL,
    CONSTRAINT "PK_CustomCommandUsages" PRIMARY KEY ("Name", "GuildId", "UserId", "UsedAt")
);
CREATE INDEX "IX_CustomCommandUsages_GuildId_Name_UsedAt"
    ON "CustomCommandUsages" ("GuildId", "Name", "UsedAt");
CREATE INDEX "IX_CustomCommandUsages_GuildId_Name_UserId"
    ON "CustomCommandUsages" ("GuildId", "Name", "UserId");
```

**Down:**
```sql
DROP TABLE "CustomCommandUsages";
DROP INDEX "IX_CustomCommands_GuildId_Name";

ALTER TABLE "CustomCommandsRole"
    DROP CONSTRAINT "FK_CustomCommandsRole_CustomCommands_Name_GuildId_CreatedAt";
ALTER TABLE "CustomCommandsRole" DROP CONSTRAINT "PK_CustomCommandsRole";
ALTER TABLE "CustomCommandsRole"
    DROP COLUMN "Name",
    DROP COLUMN "CreatedAt",
    DROP COLUMN "RoleType";
ALTER TABLE "CustomCommandsRole"
    ADD COLUMN "CustomCommandName" varchar(24) NOT NULL DEFAULT '';

ALTER TABLE "CustomCommandsRole"
    ADD CONSTRAINT "PK_CustomCommandsRole" PRIMARY KEY ("CustomCommandName", "GuildId", "RoleId");
ALTER TABLE "CustomCommandsRole"
    ADD CONSTRAINT "FK_CustomCommandsRole_CustomCommands_CustomCommandName_GuildId"
        FOREIGN KEY ("CustomCommandName", "GuildId")
        REFERENCES "CustomCommands" ("Name", "GuildId") ON DELETE CASCADE;

-- Keep only the latest version per (Name, GuildId) before restoring the old PK.
DELETE FROM "CustomCommands" cc
WHERE EXISTS (
    SELECT 1 FROM "CustomCommands" cc2
    WHERE cc2."Name" = cc."Name"
      AND cc2."GuildId" = cc."GuildId"
      AND cc2."CreatedAt" > cc."CreatedAt"
);

ALTER TABLE "CustomCommands" DROP CONSTRAINT "PK_CustomCommands";
ALTER TABLE "CustomCommands"
    DROP COLUMN "CreatedAt",
    DROP COLUMN "ModeratorId",
    DROP COLUMN "CommandType";

ALTER TABLE "CustomCommands"
    ADD COLUMN "HasMention"   boolean NOT NULL DEFAULT false,
    ADD COLUMN "HasMessage"   boolean NOT NULL DEFAULT false,
    ADD COLUMN "IsEmbedded"   boolean NOT NULL DEFAULT false,
    ADD COLUMN "RestrictedUse" boolean NOT NULL DEFAULT false;

ALTER TABLE "CustomCommands"
    ADD CONSTRAINT "PK_CustomCommands" PRIMARY KEY ("Name", "GuildId");
```

---

## Step 7 — `MessageHistory`: TPH discriminator + rename columns

**Source:** existing pending work
**Depends on:** nothing

**Up:**
```sql
-- Add string discriminator with a safe default; backfill from the integer Action enum.
-- Old enum values: 0 = Created, 1 = Edited, 2 = Deleted (DeletedByModeratorId present → DeletedByModerator).
ALTER TABLE "MessageHistory"
    ADD COLUMN "Discriminator" varchar(21) NOT NULL DEFAULT '';

UPDATE "MessageHistory"
SET "Discriminator" = CASE
    WHEN "Action" = 0 THEN 'Created'
    WHEN "Action" = 1 THEN 'Edited'
    WHEN "Action" = 2 AND "DeletedByModeratorId" IS NOT NULL THEN 'DeletedByModerator'
    ELSE 'Deleted'
END;

-- Remove the backfill default now that all rows are populated.
ALTER TABLE "MessageHistory" ALTER COLUMN "Discriminator" DROP DEFAULT;

-- Drop the TimeStamp server default; application code owns this value going forward.
ALTER TABLE "MessageHistory" ALTER COLUMN "TimeStamp" DROP DEFAULT;

ALTER TABLE "MessageHistory" RENAME COLUMN "MessageContent" TO "Content";
ALTER TABLE "MessageHistory" RENAME COLUMN "DeletedByModeratorId" TO "ModeratorId";
ALTER TABLE "MessageHistory" DROP COLUMN "Action";
```

**Down:**
```sql
ALTER TABLE "MessageHistory" ADD COLUMN "Action" int NOT NULL DEFAULT 0;

UPDATE "MessageHistory" SET "Action" = 0 WHERE "Discriminator" = 'Created';
UPDATE "MessageHistory" SET "Action" = 1 WHERE "Discriminator" = 'Edited';
UPDATE "MessageHistory" SET "Action" = 2
    WHERE "Discriminator" IN ('Deleted', 'DeletedByModerator');

ALTER TABLE "MessageHistory" ALTER COLUMN "Action" DROP DEFAULT;
ALTER TABLE "MessageHistory" ALTER COLUMN "TimeStamp" SET DEFAULT now();
ALTER TABLE "MessageHistory" RENAME COLUMN "Content" TO "MessageContent";
ALTER TABLE "MessageHistory" RENAME COLUMN "ModeratorId" TO "DeletedByModeratorId";
ALTER TABLE "MessageHistory" DROP COLUMN "Discriminator";
```

---

## Step 8 — `ProxiedMessages`: enforce `NOT NULL` on `SystemId` / `MemberId`

**Source:** existing pending work
**Depends on:** nothing

**Up:**
```sql
DELETE FROM "ProxiedMessages"
WHERE "SystemId" IS NULL OR "MemberId" IS NULL;

ALTER TABLE "ProxiedMessages"
    ALTER COLUMN "SystemId" SET NOT NULL,
    ALTER COLUMN "MemberId" SET NOT NULL;
```

**Down:**
```sql
ALTER TABLE "ProxiedMessages"
    ALTER COLUMN "SystemId" DROP NOT NULL,
    ALTER COLUMN "MemberId" DROP NOT NULL;
```

---

## Step 9 — `XpHistory`: data cleanup, retire `Created` rows

**Source:** [`spec-xp-history-redesign.md`](spec-xp-history-redesign.md)
**Depends on:** nothing (no structural schema changes; TPH columns are unchanged)

**Up:**
```sql
-- Fail fast if any Awarded row is missing an AwarderId.
-- These rows are invalid under the new model and must be resolved manually first.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM "XpHistory"
        WHERE "Type" = 'Awarded' AND "AwarderId" IS NULL
    ) THEN
        RAISE EXCEPTION
            'Data integrity error: Awarded XpHistory rows found with NULL AwarderId. '
            'Resolve these rows manually before running this migration.';
    END IF;
END $$;

-- Null out stale AwarderId values on non-Awarded rows.
UPDATE "XpHistory"
SET "AwarderId" = NULL
WHERE "Type" != 'Awarded' AND "AwarderId" IS NOT NULL;

-- Delete Created rows (zero-XP user-join records, no longer used).
DELETE FROM "XpHistory" WHERE "Type" = 'Created';
```

**Down:** No down migration. Deleted `Created` rows are intentionally removed; they hold no
analytical value. Stale `AwarderId` nulls cannot be recovered without the original data.

---

## Steps with no schema changes

The following specs require EF configuration or C# code changes only. No migration SQL is needed.

| Spec | Reason |
|---|---|
| [`spec-sin-actor-model.md`](spec-sin-actor-model.md) | `AuditActor` is stored via a value converter on the existing nullable `ModeratorId` column. No column added or removed. |
| [`spec-settings-layer-fixes.md`](spec-settings-layer-fixes.md) Fix 1 — `XpIgnoredItem` | The `Id` column stays; only the EF mapping changes to use `HasColumnName("Id")` on the typed property. |
| [`spec-settings-layer-fixes.md`](spec-settings-layer-fixes.md) Fixes 2–9 | Validation guards, method renames, style fixes, and EF value converter additions. No schema impact. |
