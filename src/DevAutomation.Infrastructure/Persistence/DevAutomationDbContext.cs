using DevAutomation.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevAutomation.Infrastructure.Persistence;

public sealed class DevAutomationDbContext : DbContext
{
    public DevAutomationDbContext(DbContextOptions<DevAutomationDbContext> options) : base(options) { }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ExecutionLog> ExecutionLogs => Set<ExecutionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("tickets");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).IsRequired();
            entity.Property(x => x.RepoUrl).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.BaseBranch).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.PrUrl).HasMaxLength(2048);
            entity.Property(x => x.FailReason).HasMaxLength(4000);
            entity.Property(x => x.ContainerId).HasMaxLength(128);
            entity.HasIndex(x => x.Status);
            entity.Navigation(x => x.ApprovalRequests).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(x => x.ExecutionLogs).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ApprovalRequest>(entity =>
        {
            entity.ToTable("approval_requests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ToolName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.InputJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.ResponderSlackId).HasMaxLength(100);
            entity.Property(x => x.SlackMessageTs).HasMaxLength(100);
            entity.Property(x => x.ResponseReason).HasMaxLength(1000);
            entity.HasIndex(x => x.TicketId);
            entity.HasOne(x => x.Ticket).WithMany(x => x.ApprovalRequests).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExecutionLog>(entity =>
        {
            entity.ToTable("execution_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Content).IsRequired();
            entity.HasIndex(x => new { x.TicketId, x.Timestamp });
            entity.HasOne(x => x.Ticket).WithMany(x => x.ExecutionLogs).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
