namespace Tiba.TradeFinanceAcl.Application;

/// <summary>
/// رابط تجمیع حساب‌ها بر مبنای حساب‌های دوما‌هیتی SGB
/// </summary>
public interface IAccountUnifier
{
    /// <param name="sgbDualKeys">کدهای 10‌رقمی دوماهیتی در SGB</param>
    /// <param name="systemAccounts">تمام حساب‌های 10‌رقمی در سیستم ما (شامل single-nature و کدهای مشتق‌شده)</param>
    /// <returns>
    /// لیست AccountAggregate که شامل:
    /// - هر حساب single-nature در SGB (روشن بودن both halves)
    /// - هر حساب dual-nature در SGB تجمیع‌شده از دو کد جداگانه در سیستم
    /// </returns>
    IEnumerable<AccountAggregate> Unify(
        IEnumerable<string> sgbDualKeys,
        IEnumerable<AccountAggregate> systemAccounts);
}