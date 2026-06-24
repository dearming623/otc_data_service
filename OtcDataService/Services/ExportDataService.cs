using OtcDataService.Models;
using OtcDataService.Models.Entities;
using OtcDataService.Repositories;
using System.Security.AccessControl;

namespace OtcDataService.Services;

public sealed class ExportDataService
{
    private readonly ConfigurationService _configurationService;
    private readonly ICleanupTrnRepository _cleanupTrnRepository;
    private readonly IProdtableRepository _prodtableRepository;
    private readonly IMktDepRepository _mktDepRepository;
    private readonly IItemCategoryRepository _itemCategoryRepository;
    private readonly RemoteUploadService _remoteUploadService;
    private readonly LogService _logService;

    public ExportDataService(
        ConfigurationService configurationService,
        ICleanupTrnRepository cleanupTrnRepository,
        IProdtableRepository prodtableRepository,
        IMktDepRepository mktDepRepository,
        IItemCategoryRepository itemCategoryRepository,
        RemoteUploadService remoteUploadService,
        LogService logService)
    {
        _configurationService = configurationService;
        _cleanupTrnRepository = cleanupTrnRepository;
        _prodtableRepository = prodtableRepository;
        _mktDepRepository = mktDepRepository;
        _itemCategoryRepository = itemCategoryRepository;
        _remoteUploadService = remoteUploadService;
        _logService = logService;
    }

    public async Task<bool> ExportAsync(
        CancellationToken cancellationToken = default,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        bool updateLastExportUtc = true)
    {
        var config = _configurationService.Current;
        var effectiveStart = startDate ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-config.SalesLookbackDays));
        var effectiveEnd = endDate ?? DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        _logService.Info(
            $"Starting catalog export for CleanupTrn sales from {effectiveStart:yyyy-MM-dd} to {effectiveEnd.AddDays(-1):yyyy-MM-dd}.");

        try
        {
            if (!AppConfiguration.TryValidateEntityId(config.EntityId, out var entityIdError))
            {
                _logService.Error($"Catalog export failed: {entityIdError}");
                return false;
            }

            Directory.CreateDirectory(config.OutputFolder);

            var pCodes = await _cleanupTrnRepository.ListDistinctCuItemsByDateRangeAsync(
                effectiveStart,
                effectiveEnd,
                cancellationToken);

            _logService.Info($"Found {pCodes.Count} distinct product code(s) in CleanupTrn.");

            var depCache = new Dictionary<int, MktDep?>();
            var categoryCache = new Dictionary<int, ItemCategory?>();
            var rowsByProductCode = new Dictionary<string, CatalogExportRow>(StringComparer.Ordinal);

            foreach (var pCode in pCodes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var products = await _prodtableRepository.ListByPCodeAsync(pCode, cancellationToken);
                if (products.Count == 0)
                {
                    _logService.Warning($"No prodtable rows for p_code {pCode}.");
                    continue;
                }

                foreach (var product in products)
                {
                    // Skip inactive products
                    var active = product.ActiveFl;  
                    if (active is not null && active.Equals("N", StringComparison.OrdinalIgnoreCase)) 
                    {
                        continue;
                    } 

                    var dep = await ResolveDepAsync(product, depCache, cancellationToken);
                    var category = await ResolveCategoryAsync(product, categoryCache, cancellationToken);
                    var row = CatalogRowMapper.Map(product, dep, category);
                    rowsByProductCode[row.ProductCode] = row;
                }
            }

            var fileName = config.BuildCatalogExportFileName(DateTime.Now);
            var filePath = Path.Combine(config.OutputFolder, fileName);

            await CsvWriter.WriteAsync(
                filePath,
                CatalogExportRow.Headers,
                rowsByProductCode.Values.Select(row => row.ToValues()),
                cancellationToken);

            if (config.FtpUploadEnabled)
            {
                var protocolLabel = RemoteUploadService.GetProtocolLabel(config.UploadProtocol);
                _logService.Info($"Uploading catalog export via {protocolLabel}...");
                await _remoteUploadService.UploadFileAsync(config, filePath, cancellationToken);
                _logService.Info($"Catalog export uploaded via {protocolLabel}: {fileName}.");
            }

            if (updateLastExportUtc)
            {
                _configurationService.Update(c => c.LastExportUtc = DateTime.UtcNow);
            }
            _logService.Info($"Catalog export completed: {rowsByProductCode.Count} row(s) written to {filePath}.");
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error($"Catalog export failed: {OdbcConnectionService.FormatOdbcError(ex)}");
            return false;
        }
    }

    private async Task<MktDep?> ResolveDepAsync(
        Prodtable product,
        Dictionary<int, MktDep?> cache,
        CancellationToken cancellationToken)
    {
        if (!product.Dep.HasValue)
        {
            return null;
        }

        var depNo = Convert.ToInt32(product.Dep.Value);
        if (cache.TryGetValue(depNo, out var cached))
        {
            return cached;
        }

        var dep = await _mktDepRepository.GetByDepNoAsync(depNo, cancellationToken);
        cache[depNo] = dep;

        if (dep is null)
        {
            _logService.Warning($"No department found for dep_no {depNo}.");
        }

        return dep;
    }

    private async Task<ItemCategory?> ResolveCategoryAsync(
        Prodtable product,
        Dictionary<int, ItemCategory?> cache,
        CancellationToken cancellationToken)
    {
        if (!product.CatNo.HasValue)
        {
            return null;
        }

        var catNo = product.CatNo.Value;
        if (cache.TryGetValue(catNo, out var cached))
        {
            return cached;
        }

        var category = await _itemCategoryRepository.GetByCatNoAsync(catNo, cancellationToken);
        cache[catNo] = category;

        if (category is null)
        {
            _logService.Warning($"No category found for cat_no {catNo}.");
        }

        return category;
    }
}
