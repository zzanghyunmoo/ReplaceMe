using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevAutomation.Infrastructure.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "tickets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "text", nullable: false),
                RepoUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                BaseBranch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                PrUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                FailReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                ContainerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_tickets", x => x.Id));

        migrationBuilder.CreateTable(
            name: "approval_requests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                ToolName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                InputJson = table.Column<string>(type: "jsonb", nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ResponderSlackId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                SlackMessageTs = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ResponseReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_approval_requests", x => x.Id);
                table.ForeignKey("FK_approval_requests_tickets_TicketId", x => x.TicketId, "tickets", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "execution_logs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Content = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_execution_logs", x => x.Id);
                table.ForeignKey("FK_execution_logs_tickets_TicketId", x => x.TicketId, "tickets", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_tickets_Status", "tickets", "Status");
        migrationBuilder.CreateIndex("IX_approval_requests_TicketId", "approval_requests", "TicketId");
        migrationBuilder.CreateIndex("IX_execution_logs_TicketId_Timestamp", "execution_logs", new[] { "TicketId", "Timestamp" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "approval_requests");
        migrationBuilder.DropTable(name: "execution_logs");
        migrationBuilder.DropTable(name: "tickets");
    }
}
