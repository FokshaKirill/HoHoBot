using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace HoHoBot.Infrastructure
{
    public class HoHoBotDbContextFactory : IDesignTimeDbContextFactory<HoHoBotDbContext>
    {
        public HoHoBotDbContext CreateDbContext(string[] args)
        {
            var basePath = Directory.GetCurrentDirectory();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");

            var optionsBuilder = new DbContextOptionsBuilder<HoHoBotDbContext>();
            optionsBuilder.UseSqlite(connectionString);

            return new HoHoBotDbContext(optionsBuilder.Options);
        }
    }
}