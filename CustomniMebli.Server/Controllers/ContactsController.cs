using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CustomniMebli.Server.Data;
using CustomniMebli.Server.Models;

namespace CustomniMebli.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContactsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ContactsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ContactRequest>>> GetAll()
    {
        return await _context.ContactRequests
  .OrderByDescending(c => c.CreatedAt)
     .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<ContactRequest>> Create(ContactRequestDto dto)
    {
    var request = new ContactRequest
        {
            Name = dto.Name,
 Phone = dto.Phone,
            Message = dto.Message
        };

        _context.ContactRequests.Add(request);
        await _context.SaveChangesAsync();

     return CreatedAtAction(nameof(GetAll), new { id = request.Id }, request);
    }

    [HttpPut("{id}/process")]
    public async Task<IActionResult> MarkAsProcessed(int id)
    {
        var request = await _context.ContactRequests.FindAsync(id);
        if (request == null) return NotFound();

     request.IsProcessed = true;
        await _context.SaveChangesAsync();
 return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var request = await _context.ContactRequests.FindAsync(id);
        if (request == null) return NotFound();

_context.ContactRequests.Remove(request);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public class ContactRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Message { get; set; }
}
