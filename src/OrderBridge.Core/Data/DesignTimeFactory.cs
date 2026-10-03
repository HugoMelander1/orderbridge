using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace OrderBridge.Core;

public class DesignTimeFactory : IDesignTimeDbContextFactory<BridgeDb>
{
    public BridgeDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<BridgeDb>().UseNpgsql("Host=localhost;Database=orderbridge;Username=orderbridge;Password=local-only").Options);
}
