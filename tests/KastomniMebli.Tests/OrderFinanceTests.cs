using KastomniMebli.Web.Crm;
using KastomniMebli.Web.Data;

namespace KastomniMebli.Tests;

public class OrderFinanceTests
{
    private static readonly string[] Materials = ["board", "edge", "hardware"];

    private static Order Sample() => new()
    {
        ContractAmount = 60_000m,
        Payments =
        [
            new Payment { Kind = "advance", Amount = 30_000m },
            new Payment { Kind = "extra", Amount = 25_000m },
            new Payment { Kind = "refund", Amount = 1_000m },
        ],
        Expenses =
        [
            new Expense { Category = "board", Amount = 12_000m },
            new Expense { Category = "edge", Amount = 1_500m },
            new Expense { Category = "hardware", Amount = 6_500m },
            new Expense { Category = "cutting", Amount = 2_000m },
            new Expense { Category = "delivery", Amount = 800m },
        ],
        Shares =
        [
            new OrderShare { Id = 1, UserId = 10, Basis = ShareBasis.MaterialsPercent, Value = 5m },   // конструктор: 5% від матеріалу
            new OrderShare { Id = 2, UserId = 20, Basis = ShareBasis.ContractPercent, Value = 10m },   // монтаж: 10% від договору
            new OrderShare { Id = 3, UserId = 30, Basis = ShareBasis.Fixed, Value = 1_500m, PaidAmount = 1_500m }, // замір: фіксовано, виплачено
        ],
    };

    [Fact]
    public void Calculates_order_money()
    {
        var m = OrderFinance.Calculate(Sample(), Materials);

        Assert.Equal(60_000m, m.Contract);
        Assert.Equal(54_000m, m.Received);          // 30 000 + 25 000 − 1 000
        Assert.Equal(6_000m, m.BalanceDue);
        Assert.Equal(22_800m, m.Expenses);
        Assert.Equal(20_000m, m.Materials);         // плита + кромка + фурнітура
        Assert.Equal(1_000m, m.Shares[0].Amount);   // 5% × 20 000
        Assert.Equal(6_000m, m.Shares[1].Amount);   // 10% × 60 000
        Assert.Equal(1_500m, m.Shares[2].Amount);
        Assert.Equal(8_500m, m.SharesTotal);
        Assert.Equal(22_700m, m.Profit);            // 54 000 − 22 800 − 8 500
    }

    [Fact]
    public void Owed_excludes_paid_shares()
    {
        var m = OrderFinance.Calculate(Sample(), Materials);

        Assert.Equal(1_000m, m.Shares[0].Owed);
        Assert.Equal(0m, m.Shares[2].Owed);
        Assert.True(m.Shares[2].IsPaid);
    }

    [Fact]
    public void Material_categories_come_from_settings()
    {
        var onlyBoard = OrderFinance.Calculate(Sample(), ["board"]);

        Assert.Equal(12_000m, onlyBoard.Materials);
        Assert.Equal(600m, onlyBoard.Shares[0].Amount);
    }

    [Fact]
    public void Overpayment_gives_negative_balance()
    {
        var order = new Order { ContractAmount = 10_000m, Payments = [new Payment { Kind = "advance", Amount = 12_000m }] };

        Assert.Equal(-2_000m, OrderFinance.Calculate(order, Materials).BalanceDue);
    }

    [Fact]
    public void Empty_order_is_all_zero()
    {
        var m = OrderFinance.Calculate(new Order(), Materials);

        Assert.Equal(0m, m.Contract);
        Assert.Equal(0m, m.Received);
        Assert.Equal(0m, m.Profit);
        Assert.Empty(m.Shares);
    }

    [Fact]
    public void Share_without_contract_is_zero_until_amount_known()
    {
        var order = new Order { Shares = [new OrderShare { Basis = ShareBasis.ContractPercent, Value = 10m }] };

        Assert.Equal(0m, OrderFinance.Calculate(order, Materials).Shares[0].Amount);
    }

    [Theory]
    [InlineData(ShareBasis.ContractPercent, 7.5, 33_333, 0, 2_499.98)]   // 2 499,975 → 2 499,98 (половина — вгору)
    [InlineData(ShareBasis.MaterialsPercent, 5, 0, 12_345.67, 617.28)]    // 617,2835
    [InlineData(ShareBasis.Fixed, 1_000.005, 0, 0, 1_000.01)]
    [InlineData("unknown", 10, 1_000, 1_000, 0)]
    public void Share_amounts_round_to_kopecks(string basis, double value, double contract, double materials, double expected)
    {
        Assert.Equal((decimal)expected, OrderFinance.ShareAmount(basis, (decimal)value, (decimal)contract, (decimal)materials));
    }
}
