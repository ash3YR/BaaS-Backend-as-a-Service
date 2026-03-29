using BaaS.Data;
using BaaS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BaaS.Controllers;

[ApiExplorerSettings(GroupName = "Data APIs")]
[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;

    public TestController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Returns all rows from the static test entity table.
    /// </summary>
    /// <returns>A JSON array of <see cref="TestEntity"/> values.</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TestEntity>>> Get()
    {
        var testEntities = await _dbContext.TestEntities
            .AsNoTracking()
            .ToListAsync();

        return Ok(testEntities);
    }

    /// <summary>
    /// Inserts a new static test entity row.
    /// </summary>
    /// <param name="testEntity">The entity to insert.</param>
    /// <returns>The inserted <see cref="TestEntity"/>.</returns>
    [HttpPost]
    public async Task<ActionResult<TestEntity>> Post(TestEntity testEntity)
    {
        _dbContext.TestEntities.Add(testEntity);
        await _dbContext.SaveChangesAsync();

        return Ok(testEntity);
    }
}
