using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SecureStatements.Infrastructure.Persistence;

// Design-time factory the EF command-line tools use to build a context for migrations. Never runs at runtime, which is why the connection string is hard-coded.
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=securestatements;Username=securestatements;Password=MYPASSWORD1234!");

        return new AppDbContext(optionsBuilder.Options);
    }
}