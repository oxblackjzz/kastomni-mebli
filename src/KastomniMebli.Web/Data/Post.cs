namespace KastomniMebli.Web.Data;

/// <summary>Пост для соцмереж: пишеться раз, публікується в кілька мереж (кожна — окремий <see cref="PostTarget"/>).</summary>
public class Post
{
    public int Id { get; set; }
    public string Text { get; set; } = "";

    /// <summary>Коли публікувати (UTC). null — щойно поставили в чергу.</summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>Чернетка не публікується, поки не натиснули «Опублікувати» / «Запланувати».</summary>
    public bool IsDraft { get; set; } = true;

    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<PostPhoto> Photos { get; set; } = [];
    public List<PostTarget> Targets { get; set; } = [];
}

public class PostPhoto
{
    public int Id { get; set; }
    public int PostId { get; set; }

    /// <summary>JPEG (обрізаний під Instagram 4:5…1.91:1, до 1440 px) — шлях у сховищі файлів.</summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// Випадковий ключ у публічній адресі фото: Instagram і Facebook забирають фото за URL без входу,
    /// а вгадати адресу чужого фото неможливо.
    /// </summary>
    public string PublicKey { get; set; } = "";

    public int SortOrder { get; set; }
}

/// <summary>Публікація поста в одну мережу: свій статус, помилка, id допису в мережі.</summary>
public class PostTarget
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public string Network { get; set; } = "";
    public string Status { get; set; } = PostTargetStatuses.Pending;
    public string? ExternalId { get; set; }
    public string? ExternalUrl { get; set; }
    public string? Error { get; set; }
    public int Attempts { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
}

public static class PostTargetStatuses
{
    public const string Pending = "pending";
    public const string Publishing = "publishing";
    public const string Done = "done";
    public const string Failed = "failed";
}
