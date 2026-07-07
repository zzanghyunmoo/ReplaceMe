using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevAutomation.Infrastructure.Migrations;

public partial class AddIssueTrackerFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "IssueTracker",
            table: "tickets",
            type: "character varying(40)",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ExternalIssueId",
            table: "tickets",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ExternalIssueKey",
            table: "tickets",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ExternalIssueUrl",
            table: "tickets",
            type: "character varying(2048)",
            maxLength: 2048,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IssueTracker", table: "tickets");
        migrationBuilder.DropColumn(name: "ExternalIssueId", table: "tickets");
        migrationBuilder.DropColumn(name: "ExternalIssueKey", table: "tickets");
        migrationBuilder.DropColumn(name: "ExternalIssueUrl", table: "tickets");
    }
}
