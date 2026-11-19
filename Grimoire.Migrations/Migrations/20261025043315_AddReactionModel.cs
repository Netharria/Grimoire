// This file is part of the Grimoire Project.
//
// Copyright (c) Netharia 2021-Present.
//
// All rights reserved.
// Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grimoire.Migrations
{
    /// <inheritdoc />
    public partial class AddReactionModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The legacy "Reaction" table already exists, so it is converted in place instead of created.
            // Its key (MessageId, EmojiId) let only one member react with each emoji, and EmojiId 0 stood for
            // every standard emoji. The new key is (MessageId, UserId, Emoji), where Emoji is Discord's markup
            // for a custom emoji and the emoji itself for a standard one. Old rows are unique under the old key,
            // so they stay unique under the new one.
            migrationBuilder.DropPrimaryKey(
                name: "PK_Reaction",
                table: "Reaction");

            migrationBuilder.RenameTable(
                name: "Reaction",
                newName: "Reactions");

            migrationBuilder.AddColumn<string>(
                name: "Emoji",
                table: "Reactions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Reactions"
                SET "Emoji" = CASE
                    WHEN "EmojiId" = 0 THEN "Name"
                    ELSE '<' || CASE WHEN "ImageUrl" LIKE '%.gif%' THEN 'a' ELSE '' END
                         || ':' || "Name" || ':' || "EmojiId"::text || '>'
                END;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Emoji",
                table: "Reactions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "EmojiId",
                table: "Reactions");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Reactions");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Reactions");

            // Legacy rows never recorded when the reaction happened; the message's creation time is the
            // closest known value.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReactedAt",
                table: "Reactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Reactions" AS reaction
                SET "ReactedAt" = message."CreatedTimestamp"
                FROM "Messages" AS message
                WHERE message."Id" = reaction."MessageId";
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ReactedAt",
                table: "Reactions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RemovedAt",
                table: "Reactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Reactions",
                table: "Reactions",
                columns: new[] { "MessageId", "UserId", "Emoji" });

            migrationBuilder.AddForeignKey(
                name: "FK_Reactions_Messages_MessageId",
                table: "Reactions",
                column: "MessageId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lossy: the old key allows one row per (MessageId, EmojiId), and every standard emoji has
            // EmojiId 0, so only one reaction per message and emoji id survives the rollback. The old table
            // could only hold reactions that are present, so soft-deleted ones are dropped.
            migrationBuilder.DropForeignKey(
                name: "FK_Reactions_Messages_MessageId",
                table: "Reactions");

            migrationBuilder.Sql("""DELETE FROM "Reactions" WHERE "RemovedAt" IS NOT NULL;""");

            migrationBuilder.DropColumn(
                name: "RemovedAt",
                table: "Reactions");

            migrationBuilder.DropColumn(
                name: "ReactedAt",
                table: "Reactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Reactions",
                table: "Reactions");

            migrationBuilder.AddColumn<decimal>(
                name: "EmojiId",
                table: "Reactions",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Reactions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Reactions",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Reactions"
                SET "EmojiId" = CASE WHEN "Emoji" LIKE '<%'
                        THEN substring("Emoji" from ':([0-9]+)>$')::numeric ELSE 0 END,
                    "Name" = CASE WHEN "Emoji" LIKE '<%'
                        THEN substring("Emoji" from '^<a?:([A-Za-z0-9_]*):') ELSE left("Emoji", 32) END,
                    "ImageUrl" = CASE WHEN "Emoji" LIKE '<%'
                        THEN 'https://cdn.discordapp.com/emojis/' || substring("Emoji" from ':([0-9]+)>$')
                             || CASE WHEN "Emoji" LIKE '<a:%' THEN '.gif' ELSE '.png' END
                        ELSE '' END;

                DELETE FROM "Reactions" AS later
                USING "Reactions" AS earlier
                WHERE later."MessageId" = earlier."MessageId"
                  AND later."EmojiId" = earlier."EmojiId"
                  AND later.ctid > earlier.ctid;
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "EmojiId",
                table: "Reactions",
                type: "numeric(20,0)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Reactions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "Reactions",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Emoji",
                table: "Reactions");

            migrationBuilder.RenameTable(
                name: "Reactions",
                newName: "Reaction");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Reaction",
                table: "Reaction",
                columns: new[] { "MessageId", "EmojiId" });
        }
    }
}
