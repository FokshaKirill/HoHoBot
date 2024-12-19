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
            // Устанавливаем базовый путь для конфигурационного файла
            var basePath = Directory.GetCurrentDirectory();

            // Строим конфигурацию, загружая appsettings.json
            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Получаем строку подключения из конфигурации
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            // Настраиваем DbContext
            var optionsBuilder = new DbContextOptionsBuilder<HoHoBotDbContext>();
            optionsBuilder.UseSqlite(connectionString);

            return new HoHoBotDbContext(optionsBuilder.Options);
        }
    }
}