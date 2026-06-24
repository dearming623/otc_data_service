using System.Globalization;
using OtcDataService.Models;
using OtcDataService.Models.Entities;

namespace OtcDataService.Services;

public static class CatalogRowMapper
{
    public const string DefaultProductCodeType = "UPC";

    public static CatalogExportRow Map(Prodtable product, MktDep? dep, ItemCategory? category) =>
        new()
        {
            ProductCode = FormatProductCode(product),
            ProductCodeType = IsPLUItem(product) ? "PLU" : DefaultProductCodeType,
            ProductName = ResolveProductName(product),
            CategoryCode = dep?.DepNo.ToString() ?? string.Empty,
            CategoryDescription = dep?.DepName ?? string.Empty,//dep?.DepDisplay ?? dep?.DepName ?? string.Empty,
            SubcategoryCode = FormatSubcategoryCode(product.CatNo),
            SubcategoryDescription = category?.CatDisplay ?? category?.CatName ?? string.Empty
        };

    public static string FormatProductCode(Prodtable product)
    {
        //Kevin要求都做法
        //// P = PLU fixed,  W = PLU Weight
        //if (IsPLUItem(product))
        //{
        //    return product.ItemNo.Trim();
        //}  
        //string barcode = product.Barcode ?? string.Empty;
        //var trimmed = barcode.Trim();
        //return trimmed.Length == 13 ? trimmed[..12] : trimmed;

        // Cody要求的做法,只需要item no
        return product.ItemNo.Trim();
    }

    public static string FormatSubcategoryCode(int? catNo) =>
        catNo.HasValue ? catNo.Value.ToString("D3", CultureInfo.InvariantCulture) : string.Empty;

    private static string ResolveProductName(Prodtable product) =>
        string.IsNullOrWhiteSpace(product.Proname) ? product.Cpronam ?? string.Empty : product.Proname;

    private static bool IsPLUItem(Prodtable product) =>
        product.AmtType?.Trim() is string amtType &&
        (amtType.Equals("P", StringComparison.OrdinalIgnoreCase) ||
         amtType.Equals("W", StringComparison.OrdinalIgnoreCase));
}
