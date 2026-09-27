namespace KastomniMebli.Web.Leads;

public static class FurnitureTypes
{
    public static readonly IReadOnlyList<(string Key, string Label)> All =
    [
        ("wardrobe", "Шафа-купе"),
        ("kitchen", "Кухня"),
        ("walk-in", "Гардеробна"),
        ("hallway", "Передпокій"),
        ("bedroom", "Дитяча / спальня"),
        ("other", "Інше"),
    ];

    public static bool IsKnown(string key) => All.Any(t => t.Key == key);

    public static string Label(string key) =>
        All.FirstOrDefault(t => t.Key == key).Label ?? key;
}
