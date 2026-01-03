using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CustomniMebli.Server.Data;
using CustomniMebli.Server.Models;

namespace CustomniMebli.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
  private readonly AppDbContext _context;

    public ReviewsController(AppDbContext context)
    {
    _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Review>>> GetAll()
    {
    return await _context.Reviews
    .Where(r => r.IsVisible)
         .OrderByDescending(r => r.CreatedAt)
   .ToListAsync();
    }

    [HttpGet("admin")]
    public async Task<ActionResult<IEnumerable<Review>>> GetAllForAdmin()
    {
        return await _context.Reviews
       .OrderByDescending(r => r.CreatedAt)
       .ToListAsync();
    }

  [HttpPost]
  public async Task<ActionResult<Review>> Create(ReviewDto dto)
    {
 var review = new Review
        {
     Name = dto.Name,
            Text = dto.Text,
            Rating = dto.Rating,
            IsVisible = dto.IsVisible
        };

     _context.Reviews.Add(review);
     await _context.SaveChangesAsync();

 return CreatedAtAction(nameof(GetAll), new { id = review.Id }, review);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, ReviewDto dto)
    {
   var review = await _context.Reviews.FindAsync(id);
   if (review == null) return NotFound();

        review.Name = dto.Name;
        review.Text = dto.Text;
  review.Rating = dto.Rating;
    review.IsVisible = dto.IsVisible;

     await _context.SaveChangesAsync();
return NoContent();
 }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
{
  var review = await _context.Reviews.FindAsync(id);
 if (review == null) return NotFound();

        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public class ReviewDto
{
    public string Name { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int Rating { get; set; } = 5;
    public bool IsVisible { get; set; } = true;
}
