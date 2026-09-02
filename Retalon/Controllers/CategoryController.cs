using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Retalon.Data;

namespace Retalon.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoryController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CategoryController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories(
        CancellationToken cancellationToken)
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.CategoryId,
                c.Name,
                c.Description
            })
            .ToListAsync(cancellationToken);

        return Ok(categories);
    }
}