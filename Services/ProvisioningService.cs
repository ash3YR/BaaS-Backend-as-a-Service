using BaaS.Models;
using Microsoft.AspNetCore.Http;

namespace BaaS.Services;

public class ProvisioningService
{
    private readonly CsvService _csvService;
    private readonly SchemaDetectionService _schemaDetectionService;
    private readonly TableService _tableService;
    private readonly DataInsertService _dataInsertService;
    private readonly DynamicDataService _dynamicDataService;

    public ProvisioningService(
        CsvService csvService,
        SchemaDetectionService schemaDetectionService,
        TableService tableService,
        DataInsertService dataInsertService,
        DynamicDataService dynamicDataService)
    {
        _csvService = csvService;
        _schemaDetectionService = schemaDetectionService;
        _tableService = tableService;
        _dataInsertService = dataInsertService;
        _dynamicDataService = dynamicDataService;
    }

    public async Task<ProvisioningResult> ProvisionAsync(
        IFormFile file,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        var parsedData = await _csvService.ParseTabularFileAsync(file, cancellationToken);
        parsedData.Schema = _schemaDetectionService.DetectSchema(parsedData);

        var result = new ProvisioningResult
        {
            Columns = parsedData.Columns,
            SampleData = parsedData.SampleData,
            Schema = parsedData.Schema
        };

        var tableResult = await _tableService.CreateTableAsync(parsedData.Schema, cancellationToken);
        if (!string.Equals(tableResult.Status, "Table created successfully", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(tableResult.TableName))
        {
            result.Status = tableResult.Status;
            result.Error = tableResult.Error;
            return result;
        }

        result.TableName = tableResult.TableName;

        try
        {
            result.RowsInserted = await _dataInsertService.InsertRowsAsync(
                tableResult.TableName,
                parsedData.Columns,
                parsedData.Schema,
                parsedData.SampleData,
                cancellationToken);

            result.PreviewRows = await _dynamicDataService.GetAllAsync(tableResult.TableName, cancellationToken);
            result.Api = BuildGeneratedApi(baseUrl, tableResult.TableName);
            result.Status = "Success";
        }
        catch (Exception exception)
        {
            result.Status = "Database unavailable";
            result.Error = exception.InnerException?.Message ?? exception.Message;
        }

        return result;
    }

    private static GeneratedApiOperations BuildGeneratedApi(string baseUrl, string tableName)
    {
        var normalizedBaseUrl = baseUrl.TrimEnd('/');
        var tableUrl = $"{normalizedBaseUrl}/api/data/{tableName}";

        return new GeneratedApiOperations
        {
            GetAll = tableUrl,
            QueryWithFiltering = $"{tableUrl}?page=1&pageSize=25&sortBy=Id&sortDirection=desc&search=Yash&filter_name=John",
            GetById = $"{tableUrl}/{{id}}",
            Create = tableUrl,
            Update = $"{tableUrl}/{{id}}",
            Delete = $"{tableUrl}/{{id}}",
            ColumnMetadata = $"{normalizedBaseUrl}/api/data/{tableName}/metadata",
            OpenApiSpec = $"{normalizedBaseUrl}/api/data/{tableName}/openapi"
        };
    }
}
