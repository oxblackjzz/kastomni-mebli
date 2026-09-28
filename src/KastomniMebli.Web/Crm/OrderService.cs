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
    DateTime CreatedAt,
    string Kind,
    DateOnly? DueDate);

public sealed class NewOrderInput
{
    public string Kind { get; set; } = OrderKinds.Retail;
    public string ClientName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string Source { get; set; } = "brother";
    public List<string> FurnitureTypes { get; set; } = [];
    public string? Comment { get; set; }
    public decimal? ContractAmount { get; set; }
    public string? Telegram { get; set; }
    public DateOnly? DueDate { get; set; }
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
    public DateOnly? DueDate { get; set; }
}

public sealed class ClientInput
{
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Telegram { get; set; }
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

/// <summary>Подія календаря: Kind = measure | install | due (термін B2B, без часу).</summary>
public sealed record CalendarEvent(DateTime Local, bool HasTime, string Kind, int OrderId, string Number, string ClientName,
    string? Phone, string? Address, string Status);

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

    public async Task<List<OrderListItem>> ListAsync(CurrentUser actor, string? status = null, string? search = null,
        string kind = OrderKinds.Retail)
    {
        await using var db = await dbs.CreateDbContextAsync();
        var q = db.Orders.AsNoTracking().Include(o => o.Client).AsQueryable();
        if (actor.IsInstaller)
            q = q.Where(o => o.Assignees.Any(a => a.UserId == actor.Id));
        else
            q = q.Where(o => o.Kind == kind);
        if (status == "open")
            q = q.Where(o => o.Status != OrderStatuses.Done && o.Status != OrderStatuses.Paid && o.Status != OrderStatuses.Cancelled);
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
            actor.CanSeeMoney ? o.ContractAmount : null, o.Source, o.CreatedAt, o.Kind, o.DueDate)).ToList();
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
        if (!Catalog.Has(OrderKinds.All, input.Kind))
            errors.Add("Невідомий вид замовлення.");
        CheckAmount(input.ContractAmount, errors, allowZero: true);
        ThrowIfAny(errors);

        await using var db = await dbs.CreateDbContextAsync();
        var client = await FindOrCreateClientAsync(db, input.ClientName.Trim(), phone, Blank(input.Address), input.Source);
        if (Blank(input.Telegram) is { } tg)
            client.Telegram = NormalizeTelegram(tg);
        var order = await CreateCoreAsync(db, client, input.Source, input.FurnitureTypes, Blank(input.Address), Blank(input.Comment),
            input.ContractAmount, leadId: null, actorId: actor.Id, kind: input.Kind, dueDate: input.DueDate);
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
        var attribution = new Attribution(lead.UtmSource, lead.UtmMedium, lead.UtmCampaign, lead.Referrer);
        order.Channel = attribution.Channel;
        order.Campaign = lead.UtmCampaign;
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

    /// <summary>Заявка з форми для меблярів → клієнт-мебляр + замовлення B2B «Нова».</summary>
    public async Task<Order> CreateB2bFromFormAsync(string name, string phone, string? telegram, IEnumerable<string> types,
        DateOnly? dueDate, string? comment, Attribution? attribution = null)
    {
        await using var db = await dbs.CreateDbContextAsync();
        var client = await FindOrCreateClientAsync(db, name, phone, address: null, source: "furniture_maker");
        if (telegram is not null)
            client.Telegram = telegram;
        var order = await CreateCoreAsync(db, client, "furniture_maker", types, address: null, comment, contract: null,
            leadId: null, actorId: null, kind: OrderKinds.B2b, dueDate: dueDate);
        if (attribution is not null)
        {
            order.Channel = attribution.Channel;
            order.Campaign = attribution.Campaign;
            await db.SaveChangesAsync();
        }
        return order;
    }

    public static string NormalizeTelegram(string value) => value.Trim().TrimStart('@');

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
        string? address, string? comment, decimal? contract, long? leadId, int? actorId,
        string kind = OrderKinds.Retail, DateOnly? dueDate = null)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var order = new Order
        {
            Number = await NextNumberAsync(db, Kyiv.ToLocal(now).Year),
            Client = client,
            LeadId = leadId,
            Kind = kind,
            DueDate = dueDate,
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
        // B2B — робота конструктора, без часток замірника й монтажника.
        if (kind == OrderKinds.Retail)
        {
            foreach (var t in await db.ShareTemplates.Where(t => t.IsActive).ToListAsync())
                order.Shares.Add(new OrderShare { UserId = t.UserId, Basis = t.Basis, Value = t.Value });
        }

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
        var contract = input.ContractAmount is null ? (decimal?)null : OrderFinance.Round(input.ContractAmount.Value);
        if (contract != order.ContractAmount)
            Audit(db, actor, order.Id, AuditActions.ContractChanged,
                $"{Money(order.ContractAmount)} → {Money(contract)}", contract);
        order.ContractAmount = contract;
        order.MeasureDate = input.MeasureDateLocal is { } m ? Kyiv.ToUtc(m) : null;
        order.InstallDate = input.InstallDateLocal is { } i ? Kyiv.ToUtc(i) : null;
        order.DueDate = input.DueDate;
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
        client.Telegram = Blank(input.Telegram) is { } tg ? NormalizeTelegram(tg) : null;
        client.Source = input.Source;
        client.Note = Blank(input.Note);
        await db.SaveChangesAsync();
    }

    /// <param name="cancelReason">Для «Скасовано»: ключ із Catalog.CancelReasons (без нього — «Інше»).</param>
    public async Task ChangeStatusAsync(CurrentUser actor, int id, string status, string? cancelReason = null, string? cancelNote = null)
    {
        if (status == OrderStatuses.Cancelled && cancelReason is not null && !Catalog.Has(Catalog.CancelReasons, cancelReason))
            throw new CrmException("Невідома причина скасування.");
        await using var db = await dbs.CreateDbContextAsync();
        var order = await db.Orders
            .Include(o => o.Client)
            .Include(o => o.Assignees).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(o => o.Id == id) ?? throw new CrmException("Замовлення не знайдено.");
        if (!OrderStatuses.IsKnown(order.Kind, status))
            throw new CrmException("Невідомий статус.");

        // Монтажник може лише закрити свій монтаж.
        if (actor.IsInstaller &&
            (order.Assignees.All(a => a.UserId != actor.Id) || order.Status != OrderStatuses.Installation || status != OrderStatuses.Done))
            throw new CrmForbiddenException();
        if (order.Status == status)
            return;

        ApplyStatus(order, status, actor.Id, time.GetUtcNow().UtcDateTime);
        if (status == OrderStatuses.Cancelled)
        {
            order.CancelReason = cancelReason ?? "other";
            order.CancelNote = Blank(cancelNote) is { } note ? note[..Math.Min(note.Length, 500)] : null;
            Audit(db, actor, order.Id, AuditActions.Cancelled,
                Catalog.Label(Catalog.CancelReasons, order.CancelReason) + (order.CancelNote is null ? "" : ": " + order.CancelNote));
        }
        else
        {
            order.CancelReason = null;
            order.CancelNote = null;
        }
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

        var rank = OrderStatuses.Rank(order.Kind, status);
        if (order.Kind == OrderKinds.B2b)
        {
            // «В роботі» — домовились, аналог договору.
            if (rank >= OrderStatuses.Rank(OrderKinds.B2b, OrderStatuses.InWork))
                order.ApprovedAt ??= nowUtc;
        }
        else
        {
            if (rank >= OrderStatuses.Rank(OrderStatuses.Measured))
                order.MeasuredAt ??= nowUtc;
            if (rank >= OrderStatuses.Rank(OrderStatuses.Approved))
                order.ApprovedAt ??= nowUtc;
        }
        order.CompletedAt = status == OrderStatuses.FinalFor(order.Kind) ? order.CompletedAt ?? nowUtc : null;
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
        var payment = new Payment
        {
            OrderId = orderId,
            Kind = input.Kind,
            Amount = OrderFinance.Round(input.Amount!.Value),
            PaidOn = input.Date,
            Method = input.Method,
            Note = Blank(input.Note),
            CreatedByUserId = actor.Id,
            CreatedAt = time.GetUtcNow().UtcDateTime,
        };
        db.Payments.Add(payment);
        Audit(db, actor, orderId, AuditActions.PaymentAdded, DescribePayment(payment), payment.Amount);
        await db.SaveChangesAsync();
    }

    public async Task DeletePaymentAsync(CurrentUser actor, int paymentId)
    {
        RequireMoney(actor);
        await using var db = await dbs.CreateDbContextAsync();
        if (await db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId) is not { } payment)
            return;
        db.Payments.Remove(payment);
        Audit(db, actor, payment.OrderId, AuditActions.PaymentDeleted, DescribePayment(payment), payment.Amount);
        await db.SaveChangesAsync();
    }

    private static string DescribePayment(Payment p) =>
        $"{Catalog.Label(Catalog.PaymentKinds, p.Kind)}, {Catalog.Label(Catalog.PaymentMethods, p.Method)}, {Kyiv.Format(p.PaidOn)}" +
        (p.Note is null ? "" : $" — {p.Note}");

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
        var expense = new Expense
        {
            OrderId = orderId,
            Category = input.Kind,
            Amount = OrderFinance.Round(input.Amount!.Value),
            SpentOn = input.Date,
            Note = Blank(input.Note),
            CreatedByUserId = actor.Id,
            CreatedAt = time.GetUtcNow().UtcDateTime,
        };
        db.Expenses.Add(expense);
        Audit(db, actor, orderId, AuditActions.ExpenseAdded, DescribeExpense(expense), expense.Amount);
        await db.SaveChangesAsync();
    }

    public async Task DeleteExpenseAsync(CurrentUser actor, int expenseId)
    {
        RequireMoney(actor);
        await using var db = await dbs.CreateDbContextAsync();
        if (await db.Expenses.FirstOrDefaultAsync(e => e.Id == expenseId) is not { } expense)
            return;
        db.Expenses.Remove(expense);
        Audit(db, actor, expense.OrderId, AuditActions.ExpenseDeleted, DescribeExpense(expense), expense.Amount);
        await db.SaveChangesAsync();
    }

    private static string DescribeExpense(Expense e) =>
        $"{Catalog.Label(Catalog.ExpenseCategories, e.Category)}, {Kyiv.Format(e.SpentOn)}" + (e.Note is null ? "" : $" — {e.Note}");

    // ---------- Частки ----------

    public async Task AddShareAsync(CurrentUser actor, int orderId, int userId, string basis, decimal value)
    {
        RequireMoney(actor);
        ValidateShare(basis, value);
        await using var db = await dbs.CreateDbContextAsync();
        await RequireOrderAsync(db, orderId);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId) ?? throw new CrmException("Користувача не знайдено.");
        db.OrderShares.Add(new OrderShare { OrderId = orderId, UserId = userId, Basis = basis, Value = value });
        Audit(db, actor, orderId, AuditActions.ShareAdded, $"{user.DisplayName}: {DescribeShare(basis, value)}");
        await db.SaveChangesAsync();
    }

    private static string DescribeShare(string basis, decimal value) =>
        basis == ShareBasis.Fixed ? Kyiv.Money(value) : $"{value.ToString("0.##", Kyiv.Culture)}% ({Catalog.Label(Catalog.ShareBases, basis)})";

    public async Task UpdateShareAsync(CurrentUser actor, int shareId, string basis, decimal value)
    {
        RequireMoney(actor);
        ValidateShare(basis, value);
        await using var db = await dbs.CreateDbContextAsync();
        var share = await db.OrderShares.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == shareId) ?? throw new CrmException("Частку не знайдено.");
        if (share.PaidAmount is not null)
            throw new CrmException("Частку вже виплачено — спершу скасуйте відмітку про виплату.");
        if (share.Basis == basis && share.Value == value)
            return;
        Audit(db, actor, share.OrderId, AuditActions.ShareChanged,
            $"{share.User.DisplayName}: {DescribeShare(share.Basis, share.Value)} → {DescribeShare(basis, value)}");
        share.Basis = basis;
        share.Value = value;
        await db.SaveChangesAsync();
    }

    public async Task DeleteShareAsync(CurrentUser actor, int shareId)
    {
        RequireMoney(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var share = await db.OrderShares.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == shareId) ?? throw new CrmException("Частку не знайдено.");
        if (share.PaidAmount is not null)
            throw new CrmException("Частку вже виплачено — спершу скасуйте відмітку про виплату.");
        db.OrderShares.Remove(share);
        Audit(db, actor, share.OrderId, AuditActions.ShareDeleted, $"{share.User.DisplayName}: {DescribeShare(share.Basis, share.Value)}");
        await db.SaveChangesAsync();
    }

    /// <summary>Відмітка «виплачено»: сума фіксується на момент виплати.</summary>
    public async Task MarkSharePaidAsync(CurrentUser actor, int shareId, DateOnly paidOn)
    {
        RequireMoney(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var share = await db.OrderShares.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == shareId) ?? throw new CrmException("Частку не знайдено.");
        if (share.PaidAmount is not null)
            return;
        var materials = await settings.GetMaterialCategoriesAsync();
        await PayShareAsync(db, actor, share, paidOn, materials);
        await db.SaveChangesAsync();
    }

    private async Task PayShareAsync(AppDbContext db, CurrentUser actor, OrderShare share, DateOnly paidOn, IReadOnlyCollection<string> materials)
    {
        var order = await db.Orders.AsNoTracking().Include(o => o.Expenses).FirstAsync(o => o.Id == share.OrderId);
        var materialsSum = order.Expenses.Where(e => materials.Contains(e.Category)).Sum(e => e.Amount);
        share.PaidAmount = OrderFinance.ShareAmount(share.Basis, share.Value, order.ContractAmount ?? 0m, materialsSum);
        share.PaidOn = paidOn;
        Audit(db, actor, share.OrderId, AuditActions.SharePaid, $"{share.User.DisplayName}, {order.Number}, {Kyiv.Format(paidOn)}", share.PaidAmount);
    }

    public async Task UnmarkSharePaidAsync(CurrentUser actor, int shareId)
    {
        RequireMoney(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var share = await db.OrderShares.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == shareId) ?? throw new CrmException("Частку не знайдено.");
        if (share.PaidAmount is null)
            return;
        Audit(db, actor, share.OrderId, AuditActions.ShareUnpaid, $"{share.User.DisplayName}, було виплачено {Kyiv.Format(share.PaidOn)}", share.PaidAmount);
        share.PaidAmount = null;
        share.PaidOn = null;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// «Виплатити все»: усі невиплачені частки людини в завершених замовленнях (як «Винні» на дашборді)
    /// позначаються виплаченими на дату. Повертає кількість і суму.
    /// </summary>
    public async Task<(int Count, decimal Total)> PayAllOwedAsync(CurrentUser actor, int userId, DateOnly paidOn)
    {
        RequireMoney(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var shares = await db.OrderShares.Include(s => s.User)
            .Where(s => s.UserId == userId && s.PaidAmount == null)
            .Where(s => db.Orders.Any(o => o.Id == s.OrderId && o.Status == OrderStatuses.Done && o.Kind == OrderKinds.Retail))
            .ToListAsync();
        var materials = await settings.GetMaterialCategoriesAsync();
        foreach (var share in shares)
            await PayShareAsync(db, actor, share, paidOn, materials);
        await db.SaveChangesAsync();
        return (shares.Count, shares.Sum(s => s.PaidAmount ?? 0));
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
        Audit(db, actor, orderId, AuditActions.TemplateApplied, "Невиплачені частки замінено на шаблон");
        await db.SaveChangesAsync();
    }

    // ---------- Календар ----------

    /// <summary>Заміри, монтажі й терміни B2B на 7 днів від weekStart (київський час). Монтажник — лише свої.</summary>
    public async Task<List<CalendarEvent>> CalendarAsync(CurrentUser actor, DateOnly weekStart)
    {
        var fromUtc = Kyiv.ToUtc(weekStart.ToDateTime(TimeOnly.MinValue));
        var toUtc = Kyiv.ToUtc(weekStart.AddDays(7).ToDateTime(TimeOnly.MinValue));
        var weekEnd = weekStart.AddDays(7);

        await using var db = await dbs.CreateDbContextAsync();
        var q = db.Orders.AsNoTracking().Include(o => o.Client)
            .Where(o => o.Status != OrderStatuses.Cancelled)
            .Where(o => (o.MeasureDate >= fromUtc && o.MeasureDate < toUtc) || (o.InstallDate >= fromUtc && o.InstallDate < toUtc)
                        || (o.DueDate >= weekStart && o.DueDate < weekEnd));
        if (!actor.CanSeeMoney)
            q = q.Where(o => o.Assignees.Any(a => a.UserId == actor.Id));
        var orders = await q.ToListAsync();

        var events = new List<CalendarEvent>();
        foreach (var o in orders)
        {
            var address = o.Address ?? o.Client.Address;
            if (o.MeasureDate is { } m && m >= fromUtc && m < toUtc)
                events.Add(new CalendarEvent(Kyiv.ToLocal(m), true, "measure", o.Id, o.Number, o.Client.Name, o.Client.Phone, address, o.Status));
            if (o.InstallDate is { } i && i >= fromUtc && i < toUtc)
                events.Add(new CalendarEvent(Kyiv.ToLocal(i), true, "install", o.Id, o.Number, o.Client.Name, o.Client.Phone, address, o.Status));
            if (actor.CanSeeMoney && o.DueDate is { } d && d >= weekStart && d < weekEnd && OrderStatuses.IsOpen(o.Status))
                events.Add(new CalendarEvent(d.ToDateTime(TimeOnly.MinValue), false, "due", o.Id, o.Number, o.Client.Name, o.Client.Phone, null, o.Status));
        }
        return events.OrderBy(e => e.Local).ThenBy(e => e.Number).ToList();
    }

    // ---------- Журнал ----------

    public async Task<List<AuditEntry>> AuditAsync(CurrentUser actor, int? orderId = null, int take = 300)
    {
        RequireMoney(actor);
        await using var db = await dbs.CreateDbContextAsync();
        var q = db.AuditLog.AsNoTracking();
        if (orderId is not null)
            q = q.Where(a => a.OrderId == orderId);
        return await q.OrderByDescending(a => a.At).ThenByDescending(a => a.Id).Take(take).ToListAsync();
    }

    private void Audit(AppDbContext db, CurrentUser actor, int? orderId, string action, string details, decimal? amount = null) =>
        db.AuditLog.Add(new AuditEntry
        {
            At = time.GetUtcNow().UtcDateTime,
            UserId = actor.Id,
            UserName = actor.Name,
            OrderId = orderId,
            Action = action,
            Details = details.Length > 1000 ? details[..1000] : details,
            Amount = amount,
        });

    private static string Money(decimal? amount) => amount is null ? "—" : Kyiv.Money(amount.Value);

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
