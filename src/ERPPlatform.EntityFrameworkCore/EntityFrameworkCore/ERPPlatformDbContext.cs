using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.EntityFrameworkCore;
using ERPPlatform.LogAnalytics;

namespace ERPPlatform.EntityFrameworkCore;

[ReplaceDbContext(typeof(IIdentityDbContext))]
[ReplaceDbContext(typeof(ITenantManagementDbContext))]
[ConnectionStringName("Default")]
public class ERPPlatformDbContext :
    AbpDbContext<ERPPlatformDbContext>,
    IIdentityDbContext,
    ITenantManagementDbContext
{
    /* Add DbSet properties for your Aggregate Roots / Entities here. */

    #region Entities from the modules

    /* Notice: We only implemented IIdentityDbContext and ITenantManagementDbContext
     * and replaced them for this DbContext. This allows you to perform JOIN
     * queries for the entities of these modules over the repositories easily. You
     * typically don't need that for other modules. But, if you need, you can
     * implement the DbContext interface of the needed module and use ReplaceDbContext
     * attribute just like IIdentityDbContext and ITenantManagementDbContext.
     *
     * More info: Replacing a DbContext of a module ensures that the related module
     * uses this DbContext on runtime. Otherwise, it will use its own DbContext class.
     */

    //Identity
    public DbSet<IdentityUser> Users { get; set; }
    public DbSet<IdentityRole> Roles { get; set; }
    public DbSet<IdentityClaimType> ClaimTypes { get; set; }
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
    public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
    public DbSet<IdentityLinkUser> LinkUsers { get; set; }
    public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
    public DbSet<IdentitySession> Sessions { get; set; }
    // Tenant Management
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }

    #endregion

    #region Log Analytics

    // Serilog ApplicationLogs table (created by Serilog PostgreSQL sink)
    public DbSet<ApplicationLog> ApplicationLogs { get; set; }

    // Actual seriloglogs table (created by Serilog.Sinks.PostgreSQL)
    public DbSet<SerilogEntry> SerilogEntries { get; set; }

    #endregion

    public ERPPlatformDbContext(DbContextOptions<ERPPlatformDbContext> options)
        : base(options)
    {

    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ConvertUtcDateTimesToUnspecified();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ConvertUtcDateTimesToUnspecified();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void ConvertUtcDateTimesToUnspecified()
    {
        var entries = ChangeTracker.Entries();
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                foreach (var property in entry.Properties)
                {
                    if (property.CurrentValue is DateTime dateTime && dateTime.Kind == DateTimeKind.Utc)
                    {
                        property.CurrentValue = DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
                    }
                }
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();
        builder.ConfigureFeatureManagement();
        builder.ConfigureTenantManagement();

        /* Configure your own tables/entities inside here */

        // Configure SerilogEntry for the seriloglogs table created by Serilog.Sinks.PostgreSQL
        // Note: Since the Serilog table doesn't have a primary key, we'll use a keyless entity
        builder.Entity<SerilogEntry>(b =>
        {
            b.ToTable("seriloglogs");
            b.HasNoKey(); // Keyless entity since Serilog table has no primary key
            
            b.Property(x => x.Message).HasColumnName("message");
            b.Property(x => x.MessageTemplate).HasColumnName("message_template");
            b.Property(x => x.Level).HasColumnName("level");
            b.Property(x => x.Timestamp).HasColumnName("timestamp");
            b.Property(x => x.Exception).HasColumnName("exception");
            b.Property(x => x.LogEvent).HasColumnName("log_event");
            
            // Add indexes for common queries (if they don't exist)
            // Note: These will only be created if you run migrations
            b.HasIndex(x => x.Timestamp).HasDatabaseName("IX_seriloglogs_timestamp");
            b.HasIndex(x => x.Level).HasDatabaseName("IX_seriloglogs_level");
        });

        //builder.Entity<YourEntity>(b =>
        //{
        //    b.ToTable(ERPPlatformConsts.DbTablePrefix + "YourEntities", ERPPlatformConsts.DbSchema);
        //    b.ConfigureByConvention(); //auto configure for the base class props
        //    //...
        //});
    }
}
