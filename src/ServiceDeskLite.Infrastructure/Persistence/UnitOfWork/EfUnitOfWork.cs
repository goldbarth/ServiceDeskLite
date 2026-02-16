using ServiceDeskLite.Application.Abstractions.Persistence;

namespace ServiceDeskLite.Infrastructure.Persistence.UnitOfWork;

public class EfUnitOfWork : IUnitOfWork
{
    private readonly ServiceDeskLiteDbContext _dbContext;

    public EfUnitOfWork(ServiceDeskLiteDbContext dbContext)
        => _dbContext = dbContext;  
    
    public Task SaveChangesAsync(CancellationToken ct = default)
        => _dbContext.SaveChangesAsync(ct);
}
