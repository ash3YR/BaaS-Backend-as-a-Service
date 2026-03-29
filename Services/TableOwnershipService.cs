using BaaS.Data;
using BaaS.Models;
using Microsoft.EntityFrameworkCore;

namespace BaaS.Services;

public class TableOwnershipService
{
    private readonly ApplicationDbContext _dbContext;

    public TableOwnershipService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RegisterOwnershipAsync(int userId, string tableName, string originalFileName, CancellationToken cancellationToken = default)
    {
        var safeTableName = SqlIdentifierSanitizer.Sanitize(tableName, "table name").ToLowerInvariant();

        var exists = await _dbContext.ProvisionedTables
            .AnyAsync(record => record.AppUserId == userId && record.TableName == safeTableName, cancellationToken);

        if (!exists)
        {
            _dbContext.ProvisionedTables.Add(new ProvisionedTableRecord
            {
                AppUserId = userId,
                TableName = safeTableName,
                OriginalFileName = originalFileName
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task EnsureOwnershipAsync(int userId, string tableName, CancellationToken cancellationToken = default)
    {
        var safeTableName = SqlIdentifierSanitizer.Sanitize(tableName, "table name").ToLowerInvariant();

        var ownsTable = await _dbContext.ProvisionedTables
            .AnyAsync(record => record.AppUserId == userId && record.TableName == safeTableName, cancellationToken);

        if (!ownsTable)
        {
            throw new UnauthorizedAccessException("You do not have access to this generated table.");
        }
    }
}
