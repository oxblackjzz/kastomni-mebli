using KastomniMebli.Web.Data;

namespace KastomniMebli.Web.Crm;

public sealed record ShareMoney(int ShareId, int UserId, string Basis, decimal Value, decimal Amount, decimal Paid, bool IsPaid)
{
    /// <summary>Скільки ще винні учаснику по цій частці.</summary>
    public decimal Owed => IsPaid ? 0 : Amount;
}

public sealed record OrderMoney(
    decimal Contract,
    decimal Received,
    decimal BalanceDue,
    decimal Expenses,
    decimal Materials,
    IReadOnlyList<ShareMoney> Shares,
    decimal SharesTotal,
    decimal Profit);

/// <summary>
/// Гроші замовлення. Уся арифметика — тут, щоб її можна було покрити тестами.
///   отримано   = аванси + доплати − повернення
///   залишок    = договір − отримано (мінус — переплата клієнта)
///   матеріал   = витрати в категоріях «матеріалу» (налаштування)
///   частка     = % від договору | % від матеріалу | фіксована сума
///   прибуток   = отримано − витрати − частки
/// Суми округлюються до копійок (половина — від нуля).
/// </summary>
public static class OrderFinance
{
    public static OrderMoney Calculate(Order order, IReadOnlyCollection<string> materialCategories)
    {
        var contract = order.ContractAmount ?? 0m;
        var received = Received(order.Payments);
        var expenses = order.Expenses.Sum(e => e.Amount);
        var materials = order.Expenses.Where(e => materialCategories.Contains(e.Category)).Sum(e => e.Amount);

        var shares = order.Shares
            .Select(s =>
            {
                var amount = ShareAmount(s.Basis, s.Value, contract, materials);
                return new ShareMoney(s.Id, s.UserId, s.Basis, s.Value, amount, s.PaidAmount ?? 0m, s.PaidAmount is not null);
            })
            .ToList();
        var sharesTotal = shares.Sum(s => s.Amount);

        return new OrderMoney(
            Contract: contract,
            Received: received,
            BalanceDue: contract - received,
            Expenses: expenses,
            Materials: materials,
            Shares: shares,
            SharesTotal: sharesTotal,
            Profit: received - expenses - sharesTotal);
    }

    public static decimal Received(IEnumerable<Payment> payments) =>
        payments.Sum(p => p.Kind == "refund" ? -p.Amount : p.Amount);

    public static decimal ShareAmount(string basis, decimal value, decimal contract, decimal materials) => basis switch
    {
        ShareBasis.ContractPercent => Round(contract * value / 100m),
        ShareBasis.MaterialsPercent => Round(materials * value / 100m),
        ShareBasis.Fixed => Round(value),
        _ => 0m,
    };

    public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
