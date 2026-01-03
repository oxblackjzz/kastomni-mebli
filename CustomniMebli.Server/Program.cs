using Microsoft.EntityFrameworkCore;
using CustomniMebli.Server.Data;
using CustomniMebli.Server.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Configure SQLite Database
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "kastomni_mebli.db");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
       .AllowAnyMethod()
      .AllowAnyHeader();
    });
});

var app = builder.Build();

// Ensure database is created and seed data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    
    // Seed admin user if not exists
  if (!db.AdminUsers.Any())
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("admin123");
      db.AdminUsers.Add(new AdminUser
        {
       Username = "admin",
            PasswordHash = hash,
         CreatedAt = DateTime.UtcNow
        });
db.SaveChanges();
        Console.WriteLine("Admin user created: admin / admin123");
    }
    
    // Seed portfolio if empty
    if (!db.PortfolioItems.Any())
    {
        db.PortfolioItems.AddRange(
            new PortfolioItem { Title = "Сучасна кухня", Category = "Кухні", ImageUrl = "", SortOrder = 1 },
            new PortfolioItem { Title = "Шафа-купе з дзеркалом", Category = "Шафи-купе", ImageUrl = "", SortOrder = 2 },
         new PortfolioItem { Title = "Компактний передпокій", Category = "Передпокої", ImageUrl = "", SortOrder = 3 }
        );
        db.SaveChanges();
    }
    
// Seed reviews if empty
    if (!db.Reviews.Any())
    {
  db.Reviews.AddRange(
            new Review { Name = "Олена К.", Text = "Чудова кухня! Все зробили якісно та вчасно.", Rating = 5, IsVisible = true },
     new Review { Name = "Віктор М.", Text = "Замовляв шафу-купе. Відмінна якість та сервіс!", Rating = 5, IsVisible = true },
            new Review { Name = "Марія С.", Text = "Дуже задоволена передпокоєм. Індивідуальний підхід.", Rating = 5, IsVisible = true }
        );
        db.SaveChanges();
    }
}

// Create wwwroot/images/portfolio folder if not exists
var wwwrootPath = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var portfolioPath = Path.Combine(wwwrootPath, "images", "portfolio");
Directory.CreateDirectory(portfolioPath);

// Enable CORS for all environments
app.UseCors("AllowAll");

app.UseDefaultFiles();
app.UseStaticFiles();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("/index.html");

app.Run();
