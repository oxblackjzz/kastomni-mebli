namespace KastomniMebli.Web.Crm;

/// <summary>Помилка, текст якої можна показати користувачу CRM як є.</summary>
public class CrmException(string message) : Exception(message);

public sealed class CrmForbiddenException() : CrmException("Недостатньо прав для цієї дії.");
