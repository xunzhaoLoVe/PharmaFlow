using System.Net;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace PharmaFlow.Application.Services;

/// <summary>采集执行引擎 — 负责 HTTP 认证、数据导出、HTML/JSON/Excel 解析</summary>
public class CollectionExecutionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SmartDataCleaner _smartDataCleaner;
    private readonly ILogger<CollectionExecutionService> _logger;

    public CollectionExecutionService(
        IHttpClientFactory httpClientFactory,
        SmartDataCleaner smartDataCleaner,
        ILogger<CollectionExecutionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _smartDataCleaner = smartDataCleaner;
        _logger = logger;
    }

    public async Task<List<CollectionRowResult>> ExecuteAsync(
        string baseUrl,
        string authType,
        string authConfigJson,
        string exportConfigJson,
        string transformConfigJson,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CollectionRowResult>();

        using var httpClient = new HttpClient();

        // 解析认证配置
        AuthConfig? authConfig = null;
        try { authConfig = JsonSerializer.Deserialize<AuthConfig>(authConfigJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); } catch (Exception ex) {
            _logger.LogWarning(ex, "AuthConfig JSON解析失败: {Json}",
                authConfigJson?.Length > 200 ? authConfigJson[..200] : authConfigJson);
        }
        _logger.LogInformation("AuthConfig解析: Token={T}, CookieName={CN}, Value={V}",
            authConfig?.Token != null,
            authConfig?.Token?.CookieName ?? "null",
            authConfig?.Token?.Value != null ? "有值" : "null");

        // 解析导出配置
        var exportConfig = ParseExportConfig(exportConfigJson);

        // 构建请求 headers（每个请求都带上）
        var requestHeaders = new Dictionary<string, string>();
        if (authConfig?.Headers != null)
        {
            foreach (var h in authConfig.Headers)
                requestHeaders[h.Key] = h.Value;
        }

        // 认证：Cookie Token 或 Header Token
        if (authConfig?.Token != null && !string.IsNullOrWhiteSpace(authConfig.Token.CookieName)
            && !string.IsNullOrWhiteSpace(authConfig.Token.Value))
        {
            requestHeaders["Cookie"] = $"{authConfig.Token.CookieName}={authConfig.Token.Value}";
            _logger.LogInformation("已设置 Cookie: {Name}", authConfig.Token.CookieName);
        }
        else if (authConfig?.Token != null && !string.IsNullOrWhiteSpace(authConfig.Token.HeaderName)
            && !string.IsNullOrWhiteSpace(authConfig.Token.Value))
        {
            requestHeaders[authConfig.Token.HeaderName] = (authConfig.Token.TokenPrefix ?? "") + authConfig.Token.Value;
            _logger.LogInformation("已设置 Header: {Name}", authConfig.Token.HeaderName);
        }

        // 导出数据（支持分页）
        var allRawRows = await ExportAllPagesAsync(httpClient, exportConfig, requestHeaders, cancellationToken);

        _logger.LogInformation("解析完成，共 {Count} 行数据", allRawRows.Count);

        // 4. 智能清洗数据（三阶段）
        var seenDedupKeys = new HashSet<string>();
        foreach (var rawRow in allRawRows)
        {
            try
            {
                var smartResult = await _smartDataCleaner.CleanAsync(rawRow, transformConfigJson, seenDedupKeys);
                results.Add(new CollectionRowResult
                {
                    RawDataJson = JsonSerializer.Serialize(rawRow),
                    CleanedDataJson = smartResult.CleanedDataJson,
                    TargetEntityType = smartResult.TargetEntityType,
                    ErrorMessage = smartResult.ErrorMessage,
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清洗数据行失败");
                results.Add(new CollectionRowResult
                {
                    RawDataJson = JsonSerializer.Serialize(rawRow),
                    ErrorMessage = $"清洗失败: {ex.Message}",
                });
            }
        }

        // 5. 批量异常检测
        var cleanedJsons = results.Select(r => r.CleanedDataJson ?? "{}").ToList();
        var enrichedJsons = _smartDataCleaner.DetectBatchAnomalies(cleanedJsons, transformConfigJson);
        for (int i = 0; i < results.Count; i++)
            results[i].CleanedDataJson = enrichedJsons[i];

        return results;
    }

    // ═══════════════════════════════════════════
    // 1. 认证
    // ═══════════════════════════════════════════

    private async Task AuthenticateAsync(
        HttpClient httpClient, CookieContainer cookieContainer,
        string baseUrl, string authConfigJson, CancellationToken ct)
    {
        AuthConfig? authConfig = null;
        try { authConfig = JsonSerializer.Deserialize<AuthConfig>(authConfigJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); } catch { }

        if (authConfig == null) return;

        // 方式1: Cookie Token（直接设置 Header，与 Python requests 库行为一致）
        if (authConfig.Token != null && !string.IsNullOrWhiteSpace(authConfig.Token.CookieName)
            && !string.IsNullOrWhiteSpace(authConfig.Token.Value))
        {
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                "Cookie", $"{authConfig.Token.CookieName}={authConfig.Token.Value}");
            _logger.LogInformation("已设置 Cookie Header: {Name}", authConfig.Token.CookieName);
            return;
        }

        // 方式2: Header Token
        if (authConfig.Token != null && !string.IsNullOrWhiteSpace(authConfig.Token.HeaderName)
            && !string.IsNullOrWhiteSpace(authConfig.Token.Value))
        {
            var headerValue = (authConfig.Token.TokenPrefix ?? "") + authConfig.Token.Value;
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(authConfig.Token.HeaderName, headerValue);
            _logger.LogInformation("已设置 Header: {Name}", authConfig.Token.HeaderName);
            return;
        }

        // 方式3: 表单登录
        if (!string.IsNullOrWhiteSpace(authConfig.LoginPath))
        {
            try
            {
                var loginDict = new Dictionary<string, string>();
                if (authConfig.LoginParams != null)
                {
                    foreach (var kvp in authConfig.LoginParams) loginDict[kvp.Key] = kvp.Value;
                }
                else
                {
                    loginDict["username"] = authConfig.Username ?? "";
                    loginDict["password"] = authConfig.Password ?? "";
                }
                if (authConfig.ExtraFields != null)
                {
                    foreach (var kvp in authConfig.ExtraFields) loginDict[kvp.Key] = kvp.Value;
                }

                var loginUrl = authConfig.LoginPath.StartsWith('/') ? authConfig.LoginPath : "/" + authConfig.LoginPath;
                var loginResp = await httpClient.PostAsync(loginUrl, new FormUrlEncodedContent(loginDict), ct);
                _logger.LogInformation("登录 {Url} 返回 {Status}", loginUrl, loginResp.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "登录失败: {Message}", ex.Message);
            }
        }
    }

    // ═══════════════════════════════════════════
    // 2. 导出数据（支持分页 JSON / 单次请求）
    // ═══════════════════════════════════════════

    private async Task<List<Dictionary<string, string>>> ExportAllPagesAsync(
        HttpClient httpClient, ExportConfig cfg, Dictionary<string, string> requestHeaders, CancellationToken ct)
    {
        var dataFormat = (cfg.DataFormat ?? "json").ToLower();
        var contentType = cfg.ContentType ?? "application/x-www-form-urlencoded";

        // Excel / HTML 单次请求
        if (dataFormat is "excel" or "xlsx" or "xls" or "html")
        {
            var bytes = await DoExportAsync(httpClient, cfg, null, requestHeaders, ct);
            if (dataFormat == "html")
                return ParseHtmlResponse(Encoding.UTF8.GetString(bytes), cfg.TableSelector, cfg.RowSelector);
            else
                return ParseExcelResponse(bytes);
        }

        // JSON 分页请求
        var allRows = new List<Dictionary<string, string>>();
        int offset = 0, total = int.MaxValue;
        int limit = cfg.Pagination?.Limit ?? 1000;
        string? dataPath = cfg.DataPath;

        while (offset < total)
        {
            var bytes = await DoExportAsync(httpClient, cfg, offset, requestHeaders, ct);
            var json = Encoding.UTF8.GetString(bytes);
            var (rows, totalCount) = ParseJsonResponse(json, dataPath);

            if (totalCount > 0) total = totalCount;
            allRows.AddRange(rows);
            offset += limit;

            _logger.LogInformation("分页进度: {Got}/{Total}", allRows.Count, total);

            if (rows.Count == 0 || rows.Count < limit) break; // 最后一页
        }

        return allRows;
    }

    private async Task<byte[]> DoExportAsync(
        HttpClient httpClient, ExportConfig cfg, int? offset,
        Dictionary<string, string> requestHeaders, CancellationToken ct)
    {
        var url = cfg.ExportPath ?? "/";
        var method = (cfg.Method ?? "GET").ToUpper();
        var contentType = cfg.ContentType ?? "application/x-www-form-urlencoded";
        var now = DateTime.Now;

        // 解析参数模板
        var resolvedParams = ResolveParams(cfg.QueryParams, now);

        HttpRequestMessage req;
        if (method == "POST" && contentType.Contains("json"))
        {
            // ── JSON Body POST ──
            var bodyObj = cfg.BodyTemplate != null
                ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(cfg.BodyTemplate)
                : new Dictionary<string, JsonElement>();

            // 替换 body 内所有字符串的日期变量
            if (bodyObj != null) ReplaceDateVars(bodyObj, now);

            // 合并 params 到 body 的 params 节点
            if (bodyObj != null && resolvedParams != null && bodyObj.TryGetValue("params", out var paramsEl))
            {
                var mergedParams = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(paramsEl.GetRawText()) ?? new();
                foreach (var kvp in resolvedParams)
                {
                    if (kvp.Value.StartsWith('[') && kvp.Value.EndsWith(']'))
                        mergedParams[kvp.Key] = JsonSerializer.Deserialize<JsonElement>(kvp.Value);
                    else
                        mergedParams[kvp.Key] = JsonSerializer.SerializeToElement(kvp.Value);
                }
                bodyObj["params"] = JsonSerializer.SerializeToElement(mergedParams);
            }

            if (offset.HasValue)
            {
                bodyObj["limit"] = JsonSerializer.SerializeToElement(cfg.Pagination?.Limit ?? 1000);
                bodyObj["limitOffset"] = JsonSerializer.SerializeToElement(offset.Value);
            }

            var jsonBody = JsonSerializer.Serialize(bodyObj);
            req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };
        }
        else if (method == "POST")
        {
            var postData = new List<KeyValuePair<string, string>>();
            if (resolvedParams != null)
            {
                foreach (var kvp in resolvedParams)
                {
                    if (kvp.Value.StartsWith('[') && kvp.Value.EndsWith(']'))
                    {
                        try
                        {
                            var arr = JsonSerializer.Deserialize<string[]>(kvp.Value);
                            if (arr != null) foreach (var item in arr) postData.Add(new(kvp.Key, item));
                        }
                        catch { postData.Add(kvp); }
                    }
                    else { postData.Add(kvp); }
                }
            }
            req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new FormUrlEncodedContent(postData)
            };
        }
        else
        {
            var qs = "";
            if (resolvedParams != null && resolvedParams.Count > 0)
                qs = "?" + string.Join("&", resolvedParams.Select(kv =>
                    $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
            req = new HttpRequestMessage(HttpMethod.Get, url + qs);
        }

        // 设置每个请求的 headers（包括 Cookie）
        foreach (var h in requestHeaders)
            req.Headers.TryAddWithoutValidation(h.Key, h.Value);

        var resp = await httpClient.SendAsync(req, ct);
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        var respCt = resp.Content.Headers.ContentType?.MediaType ?? "";
        _logger.LogInformation("导出 {Url} 返回 {Status}, {Size} bytes, CT: {CT}",
            url, resp.StatusCode, bytes.Length, respCt);

        // 调试：打印 JSON 前 500 字符
        if (respCt.Contains("json"))
        {
            var debugJson = Encoding.UTF8.GetString(bytes);
            if (debugJson.Length > 500) debugJson = debugJson[..500];
            _logger.LogInformation("JSON响应前500: {Json}", debugJson);
        }

        if (!resp.IsSuccessStatusCode)
        {
            var errBody = Encoding.UTF8.GetString(bytes);
            if (errBody.Length > 500) errBody = errBody[..500] + "...";
            throw new InvalidOperationException($"导出失败: HTTP {(int)resp.StatusCode}. {errBody}");
        }

        return bytes;
    }

    private static void ReplaceDateVars(Dictionary<string, JsonElement> obj, DateTime now)
    {
        var keys = obj.Keys.ToList();
        foreach (var key in keys)
        {
            if (obj[key].ValueKind == JsonValueKind.String)
            {
                var val = obj[key].GetString() ?? "";
                val = val.Replace("{{startDate}}", now.AddDays(-30).ToString("yyyy-MM-dd"))
                         .Replace("{{endDate}}", now.AddDays(-1).ToString("yyyy-MM-dd"))
                         .Replace("{{today}}", now.ToString("yyyy-MM-dd"))
                         .Replace("{{yesterday}}", now.AddDays(-1).ToString("yyyy-MM-dd"))
                         .Replace("{{dayBeforeYesterday}}", now.AddDays(-2).ToString("yyyy-MM-dd"));
                obj[key] = JsonSerializer.SerializeToElement(val);
            }
            else if (obj[key].ValueKind == JsonValueKind.Object)
            {
                var nested = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(obj[key].GetRawText());
                if (nested != null) { ReplaceDateVars(nested, now); obj[key] = JsonSerializer.SerializeToElement(nested); }
            }
            else if (obj[key].ValueKind == JsonValueKind.Array)
            {
                var arr = obj[key].EnumerateArray().Select(el => el.ValueKind == JsonValueKind.String ? JsonSerializer.SerializeToElement(el.GetString()!.Replace("{{startDate}}", now.AddDays(-30).ToString("yyyy-MM-dd")).Replace("{{endDate}}", now.AddDays(-1).ToString("yyyy-MM-dd")).Replace("{{today}}", now.ToString("yyyy-MM-dd")).Replace("{{yesterday}}", now.AddDays(-1).ToString("yyyy-MM-dd")).Replace("{{dayBeforeYesterday}}", now.AddDays(-2).ToString("yyyy-MM-dd"))) : el).ToList();
                obj[key] = JsonSerializer.SerializeToElement(arr);
            }
        }
    }

    private static Dictionary<string, string>? ResolveParams(
        Dictionary<string, string>? template, DateTime now)
    {
        if (template == null) return null;
        var resolved = new Dictionary<string, string>();
        foreach (var kvp in template)
        {
            resolved[kvp.Key] = kvp.Value
                .Replace("{{startDate}}", now.AddDays(-30).ToString("yyyy-MM-dd"))
                .Replace("{{endDate}}", now.AddDays(-1).ToString("yyyy-MM-dd"))
                .Replace("{{today}}", now.ToString("yyyy-MM-dd"))
                .Replace("{{yesterday}}", now.AddDays(-1).ToString("yyyy-MM-dd"))
                .Replace("{{dayBeforeYesterday}}", now.AddDays(-2).ToString("yyyy-MM-dd"));
        }
        return resolved;
    }

    // ═══════════════════════════════════════════
    // 3. 解析
    // ═══════════════════════════════════════════

    private static (List<Dictionary<string, string>> rows, int total) ParseJsonResponse(
        string json, string? dataPath)
    {
        var rows = new List<Dictionary<string, string>>();
        int total = 0;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 按 dataPath 导航，如 "data.data" 或 "data"
            var current = root;
            if (!string.IsNullOrWhiteSpace(dataPath))
            {
                foreach (var segment in dataPath.Split('.'))
                {
                    if (current.TryGetProperty(segment, out var next))
                        current = next;
                    else break;
                }
            }

            // 提取 totalCount（尝试多个字段名）
            if (root.TryGetProperty("data", out var dataEl2))
            {
                if (dataEl2.TryGetProperty("totalCount", out var tc)) total = tc.GetInt32();
                else if (dataEl2.TryGetProperty("dataCount", out var dc)) total = dc.GetInt32();
                else if (dataEl2.TryGetProperty("count", out var c)) total = c.GetInt32();
            }

            // 提取数据行数组
            JsonElement arrEl = current;
            if (current.ValueKind != JsonValueKind.Array)
            {
                // 尝试 data.data
                if (current.TryGetProperty("data", out var innerData) && innerData.ValueKind == JsonValueKind.Array)
                    arrEl = innerData;
                else if (current.TryGetProperty("rows", out var rowsEl) && rowsEl.ValueKind == JsonValueKind.Array)
                    arrEl = rowsEl;
                else if (current.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
                    arrEl = itemsEl;
                else
                    return (rows, total);
            }

            foreach (var item in arrEl.EnumerateArray())
            {
                var dict = new Dictionary<string, string>();
                foreach (var prop in item.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString() ?? "",
                        JsonValueKind.Number => prop.Value.GetRawText(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        JsonValueKind.Null => "",
                        _ => prop.Value.ToString()
                    };
                }
                if (dict.Count > 0) rows.Add(dict);
            }
        }
        catch (Exception ex)
        {
            // Don't throw - return what we have
        }
        return (rows, total);
    }

    private List<Dictionary<string, string>> ParseExcelResponse(byte[] data)
    {
        var rows = new List<Dictionary<string, string>>();
        try
        {
            using var stream = new MemoryStream(data);
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheet(1);
            var headers = new List<string>();
            bool isFirstRow = true;

            foreach (var row in sheet.RowsUsed())
            {
                if (isFirstRow)
                {
                    foreach (var cell in row.Cells()) headers.Add(cell.GetString().Trim());
                    isFirstRow = false;
                }
                else
                {
                    var dict = new Dictionary<string, string>();
                    int colIdx = 0;
                    foreach (var cell in row.Cells())
                    {
                        var key = colIdx < headers.Count ? headers[colIdx] : $"Column{colIdx}";
                        var value = cell.DataType == XLDataType.DateTime
                            ? cell.GetDateTime().ToString("yyyy-MM-dd")
                            : cell.GetString().Trim();
                        dict[key] = value;
                        colIdx++;
                    }
                    if (dict.Count > 0 && dict.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                        rows.Add(dict);
                }
            }
            _logger.LogInformation("Excel 解析完成: {Rows} 行, {Cols} 列", rows.Count, headers.Count);
        }
        catch (Exception ex) { _logger.LogError(ex, "Excel 解析失败"); }
        return rows;
    }

    private List<Dictionary<string, string>> ParseHtmlResponse(
        string html, string? tableSelector, string? rowSelector)
    {
        var rows = new List<Dictionary<string, string>>();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var table = string.IsNullOrWhiteSpace(tableSelector)
            ? doc.DocumentNode.SelectSingleNode("//table")
            : doc.DocumentNode.SelectSingleNode(tableSelector);
        if (table == null) return rows;

        var headerRow = table.SelectSingleNode(".//thead/tr") ?? table.SelectSingleNode(".//tr");
        var headers = new List<string>();
        if (headerRow != null)
            foreach (var th in headerRow.SelectNodes("th|td") ?? headerRow.ChildNodes)
                headers.Add(th.InnerText.Trim());

        var sel = string.IsNullOrWhiteSpace(rowSelector) ? ".//tbody/tr" : rowSelector;
        var dataRows = table.SelectNodes(sel);
        if (dataRows == null) return rows;

        foreach (var tr in dataRows)
        {
            var cells = tr.SelectNodes("td|th");
            if (cells == null || cells.Count == 0) continue;
            var row = new Dictionary<string, string>();
            for (int i = 0; i < cells.Count; i++)
                row[i < headers.Count ? headers[i] : $"Column{i}"] = cells[i].InnerText.Trim();
            if (row.Count > 0) rows.Add(row);
        }
        return rows;
    }

    // ═══════════════════════════════════════════
    // 配置解析
    // ═══════════════════════════════════════════

    private static ExportConfig? ParseExportConfig(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var exportEl = root.TryGetProperty("export", out var exp) ? exp : root;

            var cfg = new ExportConfig
            {
                ExportPath = GetString(exportEl, "url") ?? GetString(exportEl, "exportPath"),
                Method = GetString(exportEl, "method") ?? "GET",
                DataFormat = GetString(exportEl, "dataFormat") ?? GetString(exportEl, "response_type") ?? "json",
                ContentType = GetString(exportEl, "content_type"),
                DataPath = GetString(exportEl, "dataPath") ?? "data.data",
                TableSelector = GetString(exportEl, "tableSelector"),
                RowSelector = GetString(exportEl, "rowSelector"),
                QueryParams = ParseParams(exportEl),
            };

            // 解析 body_template（优先从外层，再从 export 内层）
            if (root.TryGetProperty("body_template", out var bt) && bt.ValueKind == JsonValueKind.Object)
            {
                cfg.BodyTemplate = bt.GetRawText();
            }
            else if (root.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.Object)
            {
                cfg.BodyTemplate = b.GetRawText();
            }
            else if (exportEl.TryGetProperty("body_template", out var bt2) && bt2.ValueKind == JsonValueKind.Object)
            {
                cfg.BodyTemplate = bt2.GetRawText();
            }

            // 解析 pagination
            if (exportEl.TryGetProperty("pagination", out var pg))
            {
                cfg.Pagination = new PaginationConfig
                {
                    Limit = pg.TryGetProperty("limit", out var l) && l.TryGetInt32(out var lv) ? lv : 1000,
                    OffsetField = GetString(pg, "offsetField") ?? "limitOffset",
                    LimitField = GetString(pg, "limitField") ?? "limit",
                };
            }

            return cfg;
        }
        catch { return null; }
    }

    private static Dictionary<string, string>? ParseParams(JsonElement el)
    {
        if (!el.TryGetProperty("params", out var paramsEl)) return null;
        var dict = new Dictionary<string, string>();
        foreach (var prop in paramsEl.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Array)
                dict[prop.Name] = JsonSerializer.Serialize(prop.Value.EnumerateArray().Select(x => x.GetString()).ToArray());
            else
                dict[prop.Name] = prop.Value.ToString();
        }
        return dict;
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    // ═══════════════════════════════════════════
    // 内部模型
    // ═══════════════════════════════════════════

    private class AuthConfig
    {
        public string? LoginPath { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public Dictionary<string, string>? ExtraFields { get; set; }
        public Dictionary<string, string>? LoginParams { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
        public TokenConfig? Token { get; set; }
    }

    private class TokenConfig
    {
        [System.Text.Json.Serialization.JsonPropertyName("cookie_name")]
        public string? CookieName { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("header_name")]
        public string? HeaderName { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("token_prefix")]
        public string? TokenPrefix { get; set; }
        public string? Value { get; set; }
    }

    private class ExportConfig
    {
        public string? ExportPath { get; set; }
        public string? Method { get; set; } = "GET";
        public string? DataFormat { get; set; } = "json";
        public string? ContentType { get; set; }
        public string? DataPath { get; set; }
        public string? TableSelector { get; set; }
        public string? RowSelector { get; set; }
        public string? BodyTemplate { get; set; }
        public Dictionary<string, string>? QueryParams { get; set; }
        public PaginationConfig? Pagination { get; set; }
    }

    private class PaginationConfig
    {
        public int Limit { get; set; } = 1000;
        public string OffsetField { get; set; } = "limitOffset";
        public string LimitField { get; set; } = "limit";
    }

    public class CollectionRowResult
    {
        public string RawDataJson { get; set; } = "{}";
        public string? CleanedDataJson { get; set; }
        public string? TargetEntityType { get; set; }
        public string? ErrorMessage { get; set; }
    }
}