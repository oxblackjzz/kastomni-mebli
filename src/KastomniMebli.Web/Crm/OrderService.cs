using KastomniMebli.Web.Crm.Auth;
using KastomniMebli.Web.Data;
using KastomniMebli.Web.Leads;
using Microsoft.EntityFrameworkCore;

namespace KastomniMebli.Web.Crm;

public sealed record OrderListItem(
    int Id,
    string Number,
    string ClientName,
    string? ClientPhone,
    string Status,
    IReadOnlyList<string> FurnitureTypes,
    string? Address,
    DateTime? MeasureDate,
    DateTime? InstallDate,
    decimal? ContractAmount,
    string Source,
    DateTime CreatedAt);

public sealed class NewOrderInput
{
    public string ClientName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string Source { get; set; } = "brother";
    public List<string> FurnitureTypes { get; set; } = [];
    public string? Comment { get; set; }
    public decimal? ContractAmount { get; set; }
}

public sealed class OrderDetailsInput
{
    public List<string> FurnitureTypes { get; set; } = [];
    public string? Address { get; set; }
    public string? Comment { get; set; }
    public string Source { get; set; } = "";
    public decimal? ContractAmount { get; set; }

    /// <summary>Київський час з поля datetime-local.</summary>
    public DateTime? MeasureDateLocal { get; set; }
    public DateTime? InstallDateLocal { get; set; }
}

public sealed class ClientInput
{
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string Source { get; set; } = "";
    public string? Note { get; set; }
}

public sealed class MoneyInput
{
    /// <summary>Вид оплати або категорія витрати.</summary>
    public string Kind { get; set; } = "";
    public decimal? Amount { get; set; }
    public DateOnly Date { get; set; }
    public string Method { get; set; } = "cash";
    public string? Note { get; set; }
}

public sealed record ClientListItem(int Id, string Name, string? Phone, string? Address, string Source, int OrdersCount, int? LastOrderId);

public sealed class OrderService(
    IDbContextFactory<AppDbContext> dbs,
    SettingsService settings,
    CrmNotifier notifier,
    TimeProvider time,
    ILogger<OrderService> log)
{
    public const decimal MaxAmount = 100_000_000m;

    // ---------- Читання ----------

    public async Task<List<OrderListItem>> ListAsync(CurrentUser actor, string? status = null, string? search = null)
    {
        await using var db = await dbs.CreateDbContextAsync();
        var q = db.Orders.AsNoTracking().Include(o => o.Client).AsQueryable();
        if (actor.IsInstaller)
            q = q.Where(o => o.Assignees.Any(a => a.UserId == actor.Id));
        if (status == "open")
            q = q.Where(o => o.Status != OrderStatuses.Done && o.Status != OrderStatuses.Cancelled);
        else if (status is not null && OrderStatuses.IsKnown(status))
            q = q.Where(o => o.Status == status);

        var orders = await q.OrderByDescending(o => o.CreatedAt).ToListAsync();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var digits = new string(s.Where(char.IsAsciiDigit).ToArray());
            orders = orders.Where(o =>
                    o.Number.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    o.Client.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (o.Address ?? o.Client.Address ?? "").Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (digits.Length >= 3 && (o.Client.Phone ?? "").Contains(digits)))
                .ToList();
        }

        return orders.Select(o => new OrderListItem(
            o.Id, o.Number, o.Client.Name, o.Client.Phone, o.Status, o.FurnitureTypes,
            o.Address ?? o.Client.Address, o.MeasureDate, o.InstallDate,
            actor.CanSeeMoney ? o.ContractAmount : null, o.Source, o.CreatedAt)).ToList();
    }

    /// <summary>
    /// Повне замовлення. Монтажнику — лише призначене йому і без грошей:
    /// суми, оплати, витрати й частки просто не завантажуються з бази.
    /// </summary>
    public async Task<Order> GetAsync(CurrentUser actor, int id)
    {
        await using var db = await dbs.CreateDbContextAsync();
        var q = db.Orders.AsNoTracking().AsSplitQuery()
            .Include(o => o.Client)
            .Include(o => o.Assignees).ThenInclude(a => a.User)
            .Include(o => o.StatusHistory)
            .Include(o => o.Files)
            .AsQueryable();
        if (actor.CanSeeMoney)
        {
            q = q.Include(o => o.Payments)
                .Include(o => o.Expenses)
                .Include(o => o.Shares).ThenInclude(s => s.User);
        }

        var order = await q.FirstOrDefaultAsync(o => o.Id == id) ?? throw new CrmException("Замовлення не знайдено.");
        if (actor.IsInstaller)
        {
            if (order.Assignees.All(a => a.UserId != actor.Id))
                throw new CrmForbiddenException();
            order.ContractAmount = null;
        }
        return order;
    }

    public async Task<List<ClientListItem>> ListClientsAsync(CurrentUser actor, string? search)
    {
        RequireMoney(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var clients = await db.Clients.AsNoTracking()
            .Select(c => new ClientListItem(c.Id, c.Name, c.Phone, c.Address, c.Source, c.Orders.Count,
                c.Orders.OrderByDescending(o => o.CreatedAt).Select(o => (int?)o.Id).FirstOrDefault()))
            .ToListAsync();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            var digits = new string(s.Where(char.IsAsciiDigit).ToArray());
            clients = clients.Where(c =>
                c.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                (c.Address ?? "").Contains(s, StringComparison.OrdinalIgnoreCase) ||
                (digits.Length >= 3 && (c.Phone ?? "").Contains(digits))).ToList();
        }
        return clients.OrderBy(c => c.Name).ToList();
    }

    public async Task<OrderMoney> MoneyAsync(Order order)
    {
        var categories = await settings.GetMaterialCategoriesAsync();
        return OrderFinance.Calculate(order, categories);
    }

    // ---------- Створення ----------

    public async Task<int> CreateAsync(CurrentUser actor, NewOrderInput input)
    {
        RequireMoney(actor);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(input.ClientName))
            errors.Add("Вкажіть ім'я клієнта.");
        string? phone = null;
        if (!string.IsNullOrWhiteSpace(input.Phone) && !PhoneNumber.TryNormalize(input.Phone, out phone!))
            errors.Add("Невірний телефон.");
        if (!Catalog.Has(Catalog.Sources, input.Source))
            errors.Add("Вкажіть, звідки клієнт.");
        CheckAmount(input.ContractAmount, errors, allowZero: true);
        ThrowIfAny(errors);

        await using var db = await dbs.CreateDbContextAsync();
        var client = await FindOrCreateClientAsync(db, input.ClientName.Trim(), phone, Blank(input.Address), input.Source);
        var order = await CreateCoreAsync(db, client, input.Source, input.FurnitureTypes, Blank(input.Address), Blank(input.Comment),
            input.ContractAmount, leadId: null, actorId: actor.Id);
        log.LogInformation("Замовлення {Number} створив {User}", order.Number, actor.Name);
        await notifier.OrderCreatedAsync(order, actor.Name);
        return order.Id;
    }

    /// <summary>Заявка з сайту → клієнт (новий або знайдений за телефоном) + замовлення «Нова заявка».</summary>
    public async Task<int> CreateFromLeadAsync(Lead lead)
    {
        await using var db = await dbs.CreateDbContextAsync();
        var existing = await db.Orders.Where(o => o.LeadId == lead.Id).Select(o => (int?)o.Id).FirstOrDefaultAsync();
        if (existing is not null)
            return existing.Value;

        var client = await FindOrCreateClientAsync(db, lead.Name, lead.Phone, lead.Location, LeadSources.Site);
        var comment = string.Join("\n", new[]
        {
            lead.Dimensions is null ? null : "Розміри: " + lead.Dimensions,
            lead.Comment,
        }.Where(x => x is not null));
        var order = await CreateCoreAsync(db, client, LeadSources.Site, lead.FurnitureTypes, lead.Location, Blank(comment),
            contract: null, leadId: lead.Id, actorId: null);
        order.CreatedAt = lead.CreatedAt;
        order.StatusHistory[0].ChangedAt = lead.CreatedAt;
        await db.SaveChangesAsync();
        return order.Id;
    }

    /// <summary>Заявки, для яких ще немає замовлення (напр. прийшли до появи CRM), — у замовлення.</summary>
    public async Task<int> BackfillLeadsAsync()
    {
        List<Lead> leads;
        await using (var db = await dbs.CreateDbContextAsync())
        {
            var linked = await db.Orders.Where(o => o.LeadId != null).Select(o => o.LeadId!.Value).ToListAsync();
            leads = await db.Leads.AsNoTracking().Where(l => !linked.Contains(l.Id)).OrderBy(l => l.Id).ToListAsync();
        }
        foreach (var lead in leads)
            await CreateFromLeadAsync(lead);
        if (leads.Count > 0)
            log.LogInformation("Створено {Count} замовлень із давніших заявок", leads.Count);
        return leads.Count;
    }

    private async Task<Client> FindOrCreateClientAsync(AppDbContext db, string name, string? phone, string? address, string source)
    {
        if (phone is not null && await db.Clients.FirstOrDefaultAsync(c => c.Phone == phone) is { } found)
        {
            if (string.IsNullOrWhiteSpace(found.Address) && address is not null)
                found.Address = address;
            return found;
        }
        return new Client
        {
            Name = name,
            Phone = phone,
            Address = address,
            Source = source,
            CreatedAt = time.GetUtcNow().UtcDateTime,
        };
    }

    private async Task<Order> CreateCoreAsync(AppDbContext db, Client client, string source, IEnumerable<string> types,
        string? address, string? comment, decimal? contract, long? leadId, int? actorId)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var order = new Order
        {
            Number = await NextNumberAsync(db, Kyiv.ToLocal(now).Year),
            Client = client,
            LeadId = leadId,
            Kind = OrderKinds.Retail,
            FurnitureTypes = types.Where(FurnitureTypes.IsKnown).Distinct().ToList(),
            Status = OrderStatuses.New,
            Source = source,
            Address = address,
            Comment = comment,
            ContractAmount = contract,
            CreatedAt = now,
            UpdatedAt = now,
        };
        order.StatusHistory.Add(new OrderStatusChange { ToStatus = OrderStatuses.New, ChangedByUserId = actorId, ChangedAt = now });
        foreach (var t in await db.ShareTemplates.Where(t => t.IsActive).ToListAsync())
            order.Shares.Add(new OrderShare { UserId = t.UserId, Basis = t.Basis, Value = t.Value });

        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    private static async Task<string> NextNumberAsync(AppDbContext db, int year)
    {
        var prefix = $"{year}-";
        var numbers = await db.Orders.Where(o => o.Number.StartsWith(prefix)).Select(o => o.Number).ToListAsync();
        var max = numbers.Select(n => int.TryParse(n[prefix.Length..], out var x) ? x : 0).DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:000}";
    }

    // ---------- Редагування ----------

    public async Task UpdateDetailsAsync(CurrentUser actor, int id, OrderDetailsInput input)
    {
        RequireMoney(actor);
        var errors = new List<string>();
        if (!Catalog.Has(Catalog.Sources, input.Source))
            errors.Add("Невідоме джерело.");
        CheckAmount(input.ContractAmount, errors, allowZero: true);
        ThrowIfAny(errors);

        await using var db = await dbs.CreateDbContextAsync();
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id) ?? throw new CrmException("Замовлення не знайдено.");
        order.FurnitureTypes = input.FurnitureTypes.Where(FurnitureTypes.IsKnown).Distinct().ToList();
        order.Address = Blank(input.Address);
        order.Comment = Blank(input.Comment);
        order.Source = input.Source;
        order.ContractAmount = input.ContractAmount is null ? null : OrderFinance.Round(input.ContractAmount.Value);
        order.MeasureDate = input.MeasureDateLocal is { } m ? Kyiv.ToUtc(m) : null;
        order.InstallDate = input.InstallDateLocal is { } i ? Kyiv.ToUtc(i) : null;
        order.UpdatedAt = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
    }

    public async Task UpdateClientAsync(CurrentUser actor, int clientId, ClientInput input)
    {
        RequireMoney(actor);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(input.Name))
            errors.Add("Вкажіть ім'я клієнта.");
        string? phone = null;
        if (!string.IsNullOrWhiteSpace(input.Phone) && !PhoneNumber.TryNormalize(input.Phone, out phone!))
            errors.Add("Невірний телефон.");
        if (!Catalog.Has(Catalog.Sources, input.Source))
            errors.Add("Невідоме джерело.");

        await using var db = await dbs.CreateDbContextAsync();
        if (phone is not null && await db.Clients.AnyAsync(c => c.Phone == phone && c.Id != clientId))
            errors.Add("Клієнт із таким телефоном уже є.");
        ThrowIfAny(errors);

        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == clientId) ?? throw new CrmException("Клієнта не знайдено.");
        client.Name = input.Name.Trim();
        client.Phone = phone;
        client.Address = Blank(input.Address);
        client.Source = input.Source;
        client.Note = Blank(input.Note);
        await db.SaveChangesAsync();
    }

    public async Task ChangeStatusAsync(CurrentUser actor, int id, string status)
    {
        if (!OrderStatuses.IsKnown(status))
            throw new CrmException("Невідомий статус.");

        await using var db = await dbs.CreateDbContextAsync();
        var order = await db.Orders
            .Include(o => o.Client)
            .Include(o => o.Assignees).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(o => o.Id == id) ?? throw new CrmException("Замовлення не знайдено.");

        // Монтажник може лише закрити свій монтаж.
        if (actor.IsInstaller &&
            (order.Assignees.All(a => a.UserId != actor.Id) || order.Status != OrderStatuses.Installation || status != OrderStatuses.Done))
            throw new CrmForbiddenException();
        if (order.Status == status)
            return;

        ApplyStatus(order, status, actor.Id, time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync();

        if (status == OrderStatuses.Installation)
            await notifier.InstallationAsync(order, order.Assignees.Where(a => a.Role == "installer").Select(a => a.User));
    }

    /// <summary>Зміна статусу з віхами для конверсії. Окремо — щоб покрити тестами.</summary>
    public static void ApplyStatus(Order order, string status, int? actorId, DateTime nowUtc)
    {
        order.StatusHistory.Add(new OrderStatusChange
        {
            OrderId = order.Id,
            FromStatus = order.Status,
            ToStatus = status,
            ChangedByUserId = actorId,
            ChangedAt = nowUtc,
        });

        var rank = OrderStatuses.Rank(status);
        if (rank >= OrderStatuses.Rank(OrderStatuses.Measured))
            order.MeasuredAt ??= nowUtc;
        if (rank >= OrderStatuses.Rank(OrderStatuses.Approved))
            order.ApprovedAt ??= nowUtc;
        order.CompletedAt = status == OrderStatuses.Done ? order.CompletedAt ?? nowUtc : null;
        order.CancelledAt = status == OrderStatuses.Cancelled ? nowUtc : null;
        order.Status = status;
        order.UpdatedAt = nowUtc;
    }

    public async Task SetAssigneeAsync(CurrentUser actor, int orderId, string role, int? userId)
    {
        RequireMoney(actor);
        if (!Catalog.Has(Catalog.AssigneeRoles, role))
            throw new CrmException("Невідома роль.");
        await using var db = await dbs.CreateDbContextAsync();
        if (!await db.Orders.AnyAsync(o => o.Id == orderId))
            throw new CrmException("Замовлення не знайдено.");
        db.OrderAssignees.RemoveRange(db.OrderAssignees.Where(a => a.OrderId == orderId && a.Role == role));
        if (userId is not null)
        {
            if (!await db.Users.AnyAsync(u => u.Id == userId && u.IsActive))
                throw new CrmException("Користувача не знайдено.");
            db.OrderAssignees.Add(new OrderAssignee { OrderId = orderId, UserId = userId.Value, Role = role });
        }
        await db.SaveChangesAsync();
    }

    // ---------- Гроші ----------

    public async Task AddPaymentAsync(CurrentUser actor, int orderId, MoneyInput input)
    {
        RequireMoney(actor);
        var errors = new List<string>();
        if (!Catalog.Has(Catalog.PaymentKinds, input.Kind))
            errors.Add("Вкажіть вид оплати.");
        if (!Catalog.Has(Catalog.PaymentMethods, input.Method))
            errors.Add("Вкажіть спосіб оплати.");
        CheckAmount(input.Amount, errors, allowZero: false, required: true);
        ThrowIfAny(errors);

        await using var db = await dbs.CreateDbContextAsync();
        await RequireOrderAsync(db, orderId);
        db.Payments.Add(new Payment
        {
            OrderId = orderId,
            Kind = input.Kind,
            Amount = OrderFinance.Round(input.Amount!.Value),
            PaidOn = input.Date,
            Method = input.Method,
            Note = Blank(input.Note),
            CreatedByUserId = actor.Id,
            CreatedAt = time.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync();
    }

    public async Task DeletePaymentAsync(CurrentUser actor, int paymentId)
    {
        RequireMoney(actor);
        await using var db = await dbs.CreateDbContextAsync();
        db.Payments.RemoveRange(db.Payments.Where(p => p.Id == paymentId));
        await db.SaveChangesAsync();
    }

    public async Task AddExpenseAsync(CurrentUser actor, int orderId, MoneyInput input)
    {
        RequireMoney(actor);
        var errors = new List<string>();
        if (!Catalog.Has(Catalog.ExpenseCategories, input.Kind))
            errors.Add("Вкажіть категорію витрати.");
        CheckAmount(input.Amount, errors, allowZero: false, required: true);
        ThrowIfAny(errors);

        await using var db = await dbs.CreateDbContextAsync();
        await RequireOrderAsync(db, orderId);
        db.Expenses.Add(new Expense
        {
            OrderId = orderId,
            Category = input.Kind,
            Amount = OrderFinance.Round(input.Amount!.Value),
            SpentOn = input.Date,
            Note = Blank(input.Note),
            CreatedByUserId = actor.Id,
            CreatedAt = time.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync();
    }

    public async Task DeleteExpenseAsync(CurrentUser actor, int expenseId)
    {
        RequireMoney(actor);
        await using var db = await dbs.CreateDbContextAsync();
        db.Expenses.RemoveRange(db.Expenses.Where(e => e.Id == expenseId));
        await db.SaveChangesAsync();
    }

    // ---------- Частки ----------

    public async Task AddShareAsync(CurrentUser actor, int orderId, int userId, string basis, decimal value)
    {
        RequireMoney(actor);
        ValidateShare(basis, value);
        await using var db = await dbs.CreateDbContextAsync();
        await RequireOrderAsync(db, orderId);
        if (!await db.Users.AnyAsync(u => u.Id == userId))
            throw new CrmException("Користувача не знайдено.");
        db.OrderShares.Add(new OrderShare { OrderId = orderId, UserId = userId, Basis = basis, Value = value });
        await db.SaveChangesAsync();
    }

    public async Task UpdateShareAsync(CurrentUser actor, int shareId, string basis, decimal value)
    {
        RequireMoney(actor);
        ValidateShare(basis, value);
        await using var db = await dbs.CreateDbContextAsync();
        var share = await db.OrderShares.FirstOrDefaultAsync(s => s.Id == shareId) ?? throw new CrmException("Частку не знайдено.");
        if (share.PaidAmount is not null)
            throw new CrmException("Частку вже виплачено — спершу скасуйте відмітку про виплату.");
        share.Basis = basis;
        share.Value = value;
        await db.SaveChangesAsync();
    }

    public async Task DeleteShareAsync(CurrentUser actor, int shareId)
    {
        RequireMoney(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var share = await db.OrderShares.FirstOrDefaultAsync(s => s.Id == shareId) ?? throw new CrmException("Частку не знайдено.");
        if (share.PaidAmount is not null)
            throw new CrmException("Частку вже виплачено — спершу скасуйте відмітку про виплату.");
        db.OrderShares.Remove(share);
        await db.SaveChangesAsync();
    }

    /// <summary>Відмітка «виплачено»: сума фіксується на момент виплати.</summary>
    public async Task MarkSharePaidAsync(CurrentUser actor, int shareId, DateOnly paidOn)
    {
        RequireMoney(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var share = await db.OrderShares.FirstOrDefaultAsync(s => s.Id == shareId) ?? throw new CrmException("Частку не знайдено.");
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Expenses)
            .FirstAsync(o => o.Id == share.OrderId);
        var materials = await settings.GetMaterialCategoriesAsync();
        var materialsSum = order.Expenses.Where(e => materials.Contains(e.Category)).Sum(e => e.Amount);
        share.PaidAmount = OrderFinance.ShareAmount(share.Basis, share.Value, order.ContractAmount ?? 0m, materialsSum);
        share.PaidOn = paidOn;
        await db.SaveChangesAsync();
    }

    public async Task UnmarkSharePaidAsync(CurrentUser actor, int shareId)
    {
        RequireMoney(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var share = await db.OrderShares.FirstOrDefaultAsync(s => s.Id == shareId) ?? throw new CrmException("Частку не знайдено.");
        share.PaidAmount = null;
        share.PaidOn = null;
        await db.SaveChangesAsync();
    }

    /// <summary>Замінити невиплачені частки замовлення на поточний шаблон.</summary>
    public async Task ApplyShareTemplateAsync(CurrentUser actor, int orderId)
    {
        RequireMoney(actor);
        await using var db = await dbs.CreateDbContextAsync();
        await RequireOrderAsync(db, orderId);
        var shares = await db.OrderShares.Where(s => s.OrderId == orderId).ToListAsync();
        db.OrderShares.RemoveRange(shares.Where(s => s.PaidAmount is null));
        var paidUsers = shares.Where(s => s.PaidAmount is not null).Select(s => s.UserId).ToHashSet();
        foreach (var t in await db.ShareTemplates.Where(t => t.IsActive).ToListAsync())
        {
            if (!paidUsers.Contains(t.UserId))
                db.OrderShares.Add(new OrderShare { OrderId = orderId, UserId = t.UserId, Basis = t.Basis, Value = t.Value });
        }
        await db.SaveChangesAsync();
    }

    public static void ValidateShare(string basis, decimal value)
    {
        if (!Catalog.Has(Catalog.ShareBases, basis))
            throw new CrmException("Невідомий спосіб розрахунку частки.");
        if (value < 0)
            throw new CrmException("Значення не може бути від'ємним.");
        if (basis != ShareBasis.Fixed && value > 100)
            throw new CrmException("Відсоток — від 0 до 100.");
        if (value > MaxAmount)
            throw new CrmException("Завелика сума.");
    }

    // ---------- Допоміжне ----------

    public static bool CanAccess(CurrentUser actor, Order order) =>
        actor.CanSeeMoney || order.Assignees.Any(a => a.UserId == actor.Id);

    private static async Task RequireOrderAsync(AppDbContext db, int orderId)
    {
        if (!await db.Orders.AnyAsync(o => o.Id == orderId))
            throw new CrmException("Замовлення не знайдено.");
    }

    private static void RequireMoney(CurrentUser actor)
    {
        if (!actor.CanSeeMoney)
            throw new CrmForbiddenException();
    }

    private static void CheckAmount(decimal? amount, List<string> errors, bool allowZero, bool required = false)
    {
        if (amount is null)
        {
            if (required)
                errors.Add("Вкажіть суму.");
            return;
        }
        if (amount < 0 || (!allowZero && amount == 0))
            errors.Add("Сума має бути більшою за нуль.");
        else if (amount > MaxAmount)
            errors.Add("Завелика сума.");
    }

    private static void ThrowIfAny(List<string> errors)
    {
        if (errors.Count > 0)
            throw new CrmException(string.Join(" ", errors));
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
