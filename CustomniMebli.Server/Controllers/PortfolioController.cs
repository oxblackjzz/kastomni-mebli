using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CustomniMebli.Server.Data;
using CustomniMebli.Server.Models;

namespace CustomniMebli.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortfolioController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public PortfolioController(AppDbContext context, IWebHostEnvironment environment)
    {
    _context = context;
    _environment = environment;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PortfolioItem>>> GetAll()
    {
        return await _context.PortfolioItems
   .Where(p => p.IsVisible)
        .OrderBy(p => p.SortOrder)
       .ToListAsync();
    }

    [HttpGet("{id}")]
 public async Task<ActionResult<PortfolioItem>> GetById(int id)
    {
        var item = await _context.PortfolioItems.FindAsync(id);
        if (item == null) return NotFound();
     return item;
    }

    [HttpPost]
    public async Task<ActionResult<PortfolioItem>> Create([FromForm] PortfolioItemDto dto)
    {
  var item = new PortfolioItem
      {
            Title = dto.Title,
            Category = dto.Category,
    Description = dto.Description,
        IsVisible = dto.IsVisible,
            SortOrder = dto.SortOrder
        };

        if (dto.Image != null)
        {
          item.ImageUrl = await SaveImage(dto.Image);
        }

      _context.PortfolioItems.Add(item);
        await _context.SaveChangesAsync();

     return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [HttpPut("{id}")]
  public async Task<IActionResult> Update(int id, [FromForm] PortfolioItemDto dto)
    {
   var item = await _context.PortfolioItems.FindAsync(id);
  if (item == null) return NotFound();

        item.Title = dto.Title;
        item.Category = dto.Category;
        item.Description = dto.Description;
        item.IsVisible = dto.IsVisible;
   item.SortOrder = dto.SortOrder;

        if (dto.Image != null)
        {
            // Delete old image
            if (!string.IsNullOrEmpty(item.ImageUrl))
            {
            DeleteImage(item.ImageUrl);
       }
  item.ImageUrl = await SaveImage(dto.Image);
        }

     await _context.SaveChangesAsync();
   return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.PortfolioItems.FindAsync(id);
      if (item == null) return NotFound();

        if (!string.IsNullOrEmpty(item.ImageUrl))
        {
   DeleteImage(item.ImageUrl);
 }

        _context.PortfolioItems.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
}

    private async Task<string> SaveImage(IFormFile file)
    {
   var uploadsFolder = Path.Combine(_environment.WebRootPath ?? "wwwroot", "images", "portfolio");
        Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

     using (var stream = new FileStream(filePath, FileMode.Create))
      {
     await file.CopyToAsync(stream);
        }

      return $"/images/portfolio/{uniqueFileName}";
    }

    private void DeleteImage(string imageUrl)
    {
        var filePath = Path.Combine(_environment.WebRootPath ?? "wwwroot", imageUrl.TrimStart('/'));
 if (System.IO.File.Exists(filePath))
        {
        System.IO.File.Delete(filePath);
        }
    }
}

public class PortfolioItemDto
{
    public string Title { get; set; } = string.Empty;
 public string Category { get; set; } = string.Empty;
  public string? Description { get; set; }
 public IFormFile? Image { get; set; }
  public bool IsVisible { get; set; } = true;
    public int SortOrder { get; set; }
}
