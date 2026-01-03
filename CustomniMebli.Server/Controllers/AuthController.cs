using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CustomniMebli.Server.Data;
using CustomniMebli.Server.Models;

namespace CustomniMebli.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;

    public AuthController(AppDbContext context)
    {
     _context = context;
    }

  [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (string.IsNullOrEmpty(dto.Username) || string.IsNullOrEmpty(dto.Password))
    {
        return BadRequest(new { success = false, message = "Username and password required" });
    }

        var user = await _context.AdminUsers
         .FirstOrDefaultAsync(u => u.Username == dto.Username);

   if (user == null)
   {
    return Unauthorized(new { success = false, message = "User not found" });
        }

        try
        {
  if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
        return Unauthorized(new { success = false, message = "Invalid password" });
       }
        }
        catch
      {
            return Unauthorized(new { success = false, message = "Password verification failed" });
        }

        return Ok(new { 
      success = true, 
        username = user.Username,
    token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{user.Username}:{DateTime.UtcNow.Ticks}"))
        });
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var adminCount = await _context.AdminUsers.CountAsync();
        return Ok(new { 
     adminUsers = adminCount,
            databaseOk = true 
        });
    }

    [HttpPost("reset-admin")]
    public async Task<IActionResult> ResetAdmin()
    {
        // Remove all admins and create fresh one
        var admins = await _context.AdminUsers.ToListAsync();
      _context.AdminUsers.RemoveRange(admins);
        
        var newAdmin = new AdminUser
     {
Username = "admin",
     PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            CreatedAt = DateTime.UtcNow
        };
        _context.AdminUsers.Add(newAdmin);
        await _context.SaveChangesAsync();
        
        return Ok(new { success = true, message = "Admin reset. Login: admin / admin123" });
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
  var user = await _context.AdminUsers
          .FirstOrDefaultAsync(u => u.Username == dto.Username);

    if (user == null || !BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
        {
  return Unauthorized(new { success = false, message = "Invalid credentials" });
  }

 user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
await _context.SaveChangesAsync();

      return Ok(new { success = true });
    }
}

public class LoginDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ChangePasswordDto
{
    public string Username { get; set; } = string.Empty;
    public string CurrentPassword { get; set; } = string.Empty;
 public string NewPassword { get; set; } = string.Empty;
}
