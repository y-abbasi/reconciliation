namespace Tiba.TradeFinanceAcl.Application;

public record AccountFilter(Range GroupKeyRange, Range? DetailKeyRange, string? ParentKey)
{
    public static readonly AccountFilter[] DefaultFilters = new[]
    {
        new AccountFilter(new Range(5, 15), null, null),
        new AccountFilter(new Range(5, 25), new Range(5, 15), null),
        new AccountFilter(new Range(0, 25), new Range(5, 25), null),
        new AccountFilter(new Range(0, 30), new Range(0, 25), null)
    };

    public AccountFilter WithParent(string? parent) => this with { ParentKey = parent };
}