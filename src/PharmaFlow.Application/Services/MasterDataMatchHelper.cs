using PharmaFlow.Domain.Enums;

namespace PharmaFlow.Application.Services;

internal static class MasterDataMatchHelper
{
    public static string NormalizeText(string? value)
        => string.Concat((value ?? string.Empty).Where(c => !char.IsWhiteSpace(c))).Trim();

    public static bool SameText(string? left, string? right)
        => string.Equals(NormalizeText(left), NormalizeText(right), StringComparison.OrdinalIgnoreCase);

    public static string DrugKey(string? name, string? specification)
        => JoinKey(NormalizeText(name), NormalizeText(specification));

    public static string DealerCustomerKey(
        string? name,
        string? drugName,
        string? province,
        DistributorRole? distributorRole,
        DistributorLevel? level)
        => JoinKey(
            NormalizeText(name),
            NormalizeText(drugName),
            NormalizeText(province),
            ((int?)distributorRole)?.ToString() ?? string.Empty,
            ((int?)level)?.ToString() ?? string.Empty);

    public static string EndCustomerKey(
        string? name,
        string? drugName,
        string? province,
        CustomerType customerType)
        => JoinKey(
            NormalizeText(name),
            NormalizeText(drugName),
            NormalizeText(province),
            ((int)customerType).ToString());

    public static string DistributionRecordKey(
        Guid drugId,
        string? batchNo,
        Guid? supplierId,
        Guid? fromCustomerId,
        Guid? toCustomerId,
        decimal quantity,
        string? deliveryOrderNo,
        DateTime distributionDate,
        string? province = null)
        => JoinKey(
            drugId.ToString(),
            NormalizeText(batchNo),
            supplierId?.ToString() ?? string.Empty,
            fromCustomerId?.ToString() ?? string.Empty,
            toCustomerId?.ToString() ?? string.Empty,
            quantity.ToString("0.####"),
            NormalizeText(deliveryOrderNo),
            distributionDate.Date.ToString("yyyy-MM-dd"),
            NormalizeText(province));

    private static string JoinKey(params string[] parts)
        => string.Join("|", parts);
}
