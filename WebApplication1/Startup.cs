using System;
using System.Configuration;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(WebApplication1.Startup))]

namespace WebApplication1
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Read connection string for Hangfire storage (our local SQL Server Practice database)
            string connectionString = ConfigurationManager.ConnectionStrings["LmsConnection"].ConnectionString;

            GlobalConfiguration.Configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
                {
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.Zero,
                    UseRecommendedIsolationLevel = true,
                    DisableGlobalLocks = true
                });

            // Start Hangfire Background Job Processing Server
            app.UseHangfireServer();

            // Mount Hangfire Real-Time Management Dashboard at /hangfire
            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                DashboardTitle = "EduPulse Hangfire Job Center"
            });

            // Register background recurring jobs (e.g. cache pre-warming)
            Services.BackgroundJobService.RegisterRecurringJobs();
        }
    }
}
