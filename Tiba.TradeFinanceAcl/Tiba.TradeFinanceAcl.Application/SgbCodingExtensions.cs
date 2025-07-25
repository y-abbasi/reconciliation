namespace Tiba.TradeFinanceAcl.Application;

public static class SgbCodingExtensions
{
    private static int CbiStartIndex(this string code) => code.Length <= 20 ? 0 : 5;
    private static int CbkStartIndex(this string code) => code.Length <= 20 ? 5 : 10;
    public static string CbiDb(this string code) => code[code.CbiStartIndex()..(code.CbiStartIndex() + 5)];
    public static string CbiCr(this string code) => code[code.CbkStartIndex()..(code.CbkStartIndex() + 5)];

    public static string ToAccountCodeDebit(this string code) => code[..CbiStartIndex(code)] + code.CbiDb() +
                                                                 code.CbiDb() + code[(CbiStartIndex(code) + 10)..];

    public static string ToAccountCodeCredit(this string code) => code[..CbiStartIndex(code)] + code.CbiCr() +
                                                                  code.CbiCr() + code[(CbiStartIndex(code) + 10)..];
}