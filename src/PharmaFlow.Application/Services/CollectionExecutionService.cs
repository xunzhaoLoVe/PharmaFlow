using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using PharmaFlow.Domain.Enums;

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
        string collectionMode,
        string authType,
        string authConfigJson,
        string exportConfigJson,
        string transformConfigJson,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CollectionRowResult>();

        // Python脚本模式：直接运行脚本接管采集流程
        if (string.Equals(collectionMode, nameof(CollectionMode.PythonScript), StringComparison.OrdinalIgnoreCase))
        {
            var pyCfg = ParseExportConfig(exportConfigJson);
            if (pyCfg != null && !string.IsNullOrWhiteSpace(pyCfg.PythonScript))
            {
                _logger.LogInformation("PythonScript 模式，执行脚本: {Path}", pyCfg.PythonScript);
                return await ExecutePythonScriptAsync(pyCfg.PythonScript);
            }
            _logger.LogWarning("PythonScript 模式但 ExportConfig 中未配置 pythonScript 路径");
            return results;
        }

        // 解析认证配置
        AuthConfig? authConfig = null;
        try { authConfig = JsonSerializer.Deserialize<AuthConfig>(authConfigJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); } catch (Exception ex) {
            _logger.LogWarning(ex, "AuthConfig JSON解析失败: {Json}",
                authConfigJson?.Length > 200 ? authConfigJson[..200] : authConfigJson);
        }

        // 解析导出配置
        var exportConfig = ParseExportConfig(exportConfigJson);

        // 构建导出请求 headers
        var requestHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (authConfig?.Headers != null)
            foreach (var h in authConfig.Headers) requestHeaders[h.Key] = h.Value;

        using var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromMinutes(3) };

        // 执行登录：支持 Set-Cookie 捕获 + JSON 响应 token 提取
        if (authConfig != null && !string.IsNullOrWhiteSpace(authConfig.LoginPath))
        {
            var loginResult = await ExecuteLoginAsync(httpClient, authConfig, cancellationToken);
            if (!string.IsNullOrWhiteSpace(loginResult.Cookies))
                requestHeaders["Cookie"] = MergeCookies(GetHeaderValue(requestHeaders, "Cookie"), loginResult.Cookies)!;
            if (!string.IsNullOrWhiteSpace(loginResult.ExtractedToken))
                ApplyTokenTargets(requestHeaders, loginResult.ExtractedToken!, authConfig.TokenExtract?.Targets);
        }

        // 认证：兼容旧配置的静态 Cookie Token 或 Header Token
        if (authConfig?.Token != null && !string.IsNullOrWhiteSpace(authConfig.Token.CookieName)
            && !string.IsNullOrWhiteSpace(authConfig.Token.Value))
        {
            var tokenCookie = $"{authConfig.Token.CookieName}={authConfig.Token.Value}";
            requestHeaders["Cookie"] = MergeCookies(GetHeaderValue(requestHeaders, "Cookie"), tokenCookie)!;
            _logger.LogInformation("已设置 Cookie: {Name}", authConfig.Token.CookieName);
        }
        else if (authConfig?.Token != null && !string.IsNullOrWhiteSpace(authConfig.Token.HeaderName)
            && !string.IsNullOrWhiteSpace(authConfig.Token.Value))
        {
            requestHeaders[authConfig.Token.HeaderName] = BuildTokenValue(authConfig.Token.Value, authConfig.Token.TokenPrefix);
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

        // Excel / HTML / BusinessData 单次请求
        if (dataFormat is "excel" or "xlsx" or "xls" or "html" or "businessdata")
        {
            var bytes = await DoExportAsync(httpClient, cfg, null, requestHeaders, ct);
            if (dataFormat == "html")
            {
                _logger.LogInformation("HTML encoding config: '{Enc}', using: {Actual}",
                    cfg.Encoding ?? "null",
                    !string.IsNullOrWhiteSpace(cfg.Encoding) ? cfg.Encoding : "UTF8(default)");
                var enc = !string.IsNullOrWhiteSpace(cfg.Encoding)
                    ? Encoding.GetEncoding(cfg.Encoding)
                    : Encoding.UTF8;
                return ParseHtmlResponse(enc.GetString(bytes), cfg.TableSelector, cfg.RowSelector);
            }
            if (dataFormat == "businessdata")
            {
                var enc = !string.IsNullOrWhiteSpace(cfg.Encoding) ? Encoding.GetEncoding(cfg.Encoding) : Encoding.UTF8;
                return ApplyFieldMap(ParseBusinessDataResponse(enc.GetString(bytes), cfg.BusinessDataColumns, cfg.BusinessDataColumnMap), cfg.FieldMap);
            }
            return ApplyFieldMap(ParseExcelResponse(bytes), cfg.FieldMap);
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
            rows = ApplyFieldMap(rows, cfg.FieldMap);

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

        // 如果配置了 Python 脚本文件，直接调用脚本获取数据
        if (!string.IsNullOrWhiteSpace(cfg.PythonScript))
        {
            var psi = new System.Diagnostics.ProcessStartInfo("python", "\"" + cfg.PythonScript + "\"")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            var proc = System.Diagnostics.Process.Start(psi);
            var scriptOutput = await proc!.StandardOutput.ReadToEndAsync();
            proc.WaitForExit();
            return Encoding.UTF8.GetBytes(scriptOutput ?? "[]");
        }

        // 解析参数模板
        var resolvedParams = ResolveParams(cfg.QueryParams, now);

        // 如果需要 ViewState，先 GET 页面提取
        string? viewState = null, viewStateGen = null, eventVal = null;
        if (!string.IsNullOrWhiteSpace(cfg.ViewStatePageUrl))
        {
            try
            {
                var vsUrl = cfg.ViewStatePageUrl.StartsWith('/') ? cfg.ViewStatePageUrl : cfg.ViewStatePageUrl;
                var vsResp = await httpClient.GetAsync(vsUrl, ct);
                var vsBytes = await vsResp.Content.ReadAsByteArrayAsync();
                var vsHtml = Encoding.GetEncoding("gb2312").GetString(vsBytes);
                viewState = Regex.Match(vsHtml, "id=\"__VIEWSTATE\" value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
                viewStateGen = Regex.Match(vsHtml, "id=\"__VIEWSTATEGENERATOR\" value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
                eventVal = Regex.Match(vsHtml, "id=\"__EVENTVALIDATION\" value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
            }
            catch { /* ignore */ }
        }

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

        // 合并 ViewState 到 Form POST
        if (viewState != null && method == "POST" && req.Content is FormUrlEncodedContent)
        {
            var formDict = new Dictionary<string, string>();
            if (resolvedParams != null) foreach (var kvp in resolvedParams) formDict[kvp.Key] = kvp.Value;
            formDict["__VIEWSTATE"] = viewState;
            formDict["__VIEWSTATEGENERATOR"] = viewStateGen ?? "";
            formDict["__EVENTVALIDATION"] = eventVal ?? "";
            req.Content = new FormUrlEncodedContent(formDict);
        }

        var resp = await httpClient.SendAsync(req, ct);
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        var respCt = resp.Content.Headers.ContentType?.MediaType ?? "";
        _logger.LogInformation("导出 {Url} 返回 {Status}, {Size} bytes, CT: {CT}",
            url, resp.StatusCode, bytes.Length, respCt);

        // 调试：打印 HTML/JSON 前 500 字符
        if (respCt.Contains("html") || respCt.Contains("json"))
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

        ThrowIfAuthFailureResponse(respCt, bytes);

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
                         .Replace("{{15daysAgo}}", now.AddDays(-15).ToString("yyyy-MM-dd"))
                         .Replace("{{endDate}}", now.AddDays(-1).ToString("yyyy-MM-dd"))
                         .Replace("{{today}}", now.ToString("yyyy-MM-dd"))
                         .Replace("{{yesterday}}", now.AddDays(-1).ToString("yyyy-MM-dd"))
                         .Replace("{{dayBeforeYesterday}}", now.AddDays(-2).ToString("yyyy-MM-dd"))
                         .Replace("{{7daysAgo}}", now.AddDays(-7).ToString("yyyy-MM-dd"))
                         .Replace("{{15daysAgo}}", now.AddDays(-15).ToString("yyyy-MM-dd"));
                val = ReplaceMonthVars(val, now);
                obj[key] = JsonSerializer.SerializeToElement(val);
            }
            else if (obj[key].ValueKind == JsonValueKind.Object)
            {
                var nested = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(obj[key].GetRawText());
                if (nested != null) { ReplaceDateVars(nested, now); obj[key] = JsonSerializer.SerializeToElement(nested); }
            }
            else if (obj[key].ValueKind == JsonValueKind.Array)
            {
                var arrList = new List<JsonElement>();
                foreach (var el in obj[key].EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.String)
                    {
                        var arrVal = el.GetString()!.Replace("{{startDate}}", now.AddDays(-30).ToString("yyyy-MM-dd")).Replace("{{endDate}}", now.AddDays(-1).ToString("yyyy-MM-dd")).Replace("{{today}}", now.ToString("yyyy-MM-dd")).Replace("{{yesterday}}", now.AddDays(-1).ToString("yyyy-MM-dd")).Replace("{{dayBeforeYesterday}}", now.AddDays(-2).ToString("yyyy-MM-dd")).Replace("{{7daysAgo}}", now.AddDays(-7).ToString("yyyy-MM-dd")).Replace("{{15daysAgo}}", now.AddDays(-15).ToString("yyyy-MM-dd"));
                        arrList.Add(JsonSerializer.SerializeToElement(ReplaceMonthVars(arrVal, now)));
                    }
                    else if (el.ValueKind == JsonValueKind.Object)
                    {
                        var nested = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(el.GetRawText());
                        if (nested != null) { ReplaceDateVars(nested, now); arrList.Add(JsonSerializer.SerializeToElement(nested)); }
                    }
                    else arrList.Add(el);
                }
                obj[key] = JsonSerializer.SerializeToElement(arrList);
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
            var value = kvp.Value
                .Replace("{{startDate}}", now.AddDays(-30).ToString("yyyy-MM-dd"))
                .Replace("{{endDate}}", now.AddDays(-1).ToString("yyyy-MM-dd"))
                .Replace("{{today}}", now.ToString("yyyy-MM-dd"))
                .Replace("{{yesterday}}", now.AddDays(-1).ToString("yyyy-MM-dd"))
                .Replace("{{dayBeforeYesterday}}", now.AddDays(-2).ToString("yyyy-MM-dd"))
                .Replace("{{7daysAgo}}", now.AddDays(-7).ToString("yyyy-MM-dd"))
                .Replace("{{15daysAgo}}", now.AddDays(-15).ToString("yyyy-MM-dd"));
            resolved[kvp.Key] = ReplaceMonthVars(value, now);
        }
        return resolved;
    }

    private static string ReplaceMonthVars(string value, DateTime now)
    {
        var firstThisMonth = new DateTime(now.Year, now.Month, 1);
        var firstLastMonth = firstThisMonth.AddMonths(-1);
        var lastLastMonth = firstThisMonth.AddDays(-1);
        return value
            .Replace("{{lastMonthStart}}", firstLastMonth.ToString("yyyy-MM-dd"))
            .Replace("{{lastMonthEnd}}", lastLastMonth.ToString("yyyy-MM-dd"))
            .Replace("{{thisMonthStart}}", firstThisMonth.ToString("yyyy-MM-dd"));
    }

    // ═══════════════════════════════════════════
    // 3. 解析
    // ═══════════════════════════════════════════

    private static List<Dictionary<string, string>> ApplyFieldMap(List<Dictionary<string, string>> rows, Dictionary<string, string>? fieldMap)
    {
        if (fieldMap == null || fieldMap.Count == 0) return rows;
        var mappedRows = new List<Dictionary<string, string>>();
        foreach (var row in rows)
        {
            var mapped = new Dictionary<string, string>();
            foreach (var kvp in fieldMap)
                mapped[kvp.Value] = row.TryGetValue(kvp.Key, out var value) ? value : "";
            mappedRows.Add(mapped);
        }
        return mappedRows;
    }

    private static (List<Dictionary<string, string>> rows, int total) ParseJsonResponse(
        string json, string? dataPath)
    {
        var rows = new List<Dictionary<string, string>>();
        int total = 0;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 特殊格式: chartMain.data = [[{v:val},...],...]
            if (root.TryGetProperty("chartMain", out var cm))
            {
                if (cm.TryGetProperty("data", out var cmData) && cmData.ValueKind == JsonValueKind.Array &&
                    cm.TryGetProperty("column", out var col))
                {
                    var colNames = new List<string>();
                    if (col.TryGetProperty("values", out var colVals) && colVals.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var colGroup in colVals.EnumerateArray())
                            foreach (var colDef in colGroup.EnumerateArray())
                                colNames.Add(colDef.TryGetProperty("title", out var t) ? t.GetString() ?? "?" : "?");
                    }
                    foreach (var row in cmData.EnumerateArray())
                    {
                        var dict = new Dictionary<string, string>();
                        int ci = 0;
                        foreach (var cell in row.EnumerateArray())
                        {
                            var key = ci < colNames.Count ? colNames[ci] : $"Col{ci}";
                            var val = cell.ValueKind == JsonValueKind.Object && cell.TryGetProperty("v", out var v)
                        ? (v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.GetRawText())
                        : cell.ValueKind == JsonValueKind.Null ? "" : cell.GetRawText();
                            dict[key] = val;
                            ci++;
                        }
                        if (dict.Count > 0) rows.Add(dict);
                    }
                    return (rows, rows.Count);
                }
            }

            // 按 dataPath 导航
            var current = root;
            if (!string.IsNullOrWhiteSpace(dataPath))
            {
                foreach (var segment in dataPath.Split('.'))
                {
                    if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, out var index))
                    {
                        current = index >= 0 && index < current.GetArrayLength() ? current[index] : current;
                    }
                    else if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out var next)) current = next;
                    else break;
                }
            }

            // 提取 totalCount
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
                if (current.TryGetProperty("data", out var innerData) && innerData.ValueKind == JsonValueKind.Array) arrEl = innerData;
                else if (current.TryGetProperty("rows", out var rowsEl) && rowsEl.ValueKind == JsonValueKind.Array) arrEl = rowsEl;
                else if (current.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array) arrEl = itemsEl;
                else return (rows, total);
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
                        JsonValueKind.True => "true", JsonValueKind.False => "false",
                        JsonValueKind.Null => "", _ => prop.Value.ToString()
                    };
                }
                if (dict.Count > 0) rows.Add(dict);
            }
        }
        catch (Exception) { }
        return (rows, total);
    }

    private static List<Dictionary<string, string>> ParseBusinessDataResponse(string text, List<string>? columns, Dictionary<string, int>? columnMap)
    {
        var rows = new List<Dictionary<string, string>>();
        try
        {
            // 南华接口返回形如: ({text:'BusinessData[\"BC_bill_sale_dtl\"]=[[...]];\n...',status:1})
            var wrapper = Regex.Match(text, @"\(\{text:'(?<text>.*)',status:\d+\}\)", RegexOptions.Singleline);
            if (wrapper.Success)
                text = UnescapeJsString(wrapper.Groups["text"].Value);

            var match = Regex.Match(text, "BusinessData\\[\"[^\"]+\"\\]\\s*=\\s*(\\[.*?\\])\\s*;", RegexOptions.Singleline);
            if (!match.Success) return rows;
            using var doc = JsonDocument.Parse(match.Groups[1].Value);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return rows;
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var dict = new Dictionary<string, string>();
                if (item.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in item.EnumerateObject())
                        dict[prop.Name] = JsonElementToString(prop.Value);
                }
                else if (item.ValueKind == JsonValueKind.Array)
                {
                    var cells = item.EnumerateArray().Select(JsonElementToString).ToList();
                    if (columnMap != null && columnMap.Count > 0)
                    {
                        foreach (var kvp in columnMap)
                            if (kvp.Value >= 0 && kvp.Value < cells.Count) dict[kvp.Key] = cells[kvp.Value];
                    }
                    else
                    {
                        for (var idx = 0; idx < cells.Count; idx++)
                        {
                            var hasConfiguredColumns = columns != null && idx < columns.Count;
                            var key = hasConfiguredColumns ? columns![idx] : $"Col{idx}";
                            if (hasConfiguredColumns && string.IsNullOrWhiteSpace(key)) continue;
                            dict[key] = cells[idx];
                        }
                    }
                }
                if (dict.Count > 0) rows.Add(dict);
            }
        }
        catch { }
        return rows;
    }

    private static string UnescapeJsString(string value)
    {
        return value.Replace("\\\\", "\\")
            .Replace("\\'", "'")
            .Replace("\\\"", "\"")
            .Replace("\\r", "\r")
            .Replace("\\n", "\n")
            .Replace("\\t", "\t");
    }

    private static string JsonElementToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "",
            _ => element.ToString()
        };
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

        // 智能表头检测：找单元格最多的行（避免把标题行当表头）
        HtmlNode? headerRow = null;
        int maxCells = 0;
        var allTrs = table.SelectNodes(".//tr");
        if (allTrs != null)
        {
            foreach (var tr in allTrs)
            {
                var tds = tr.SelectNodes("td|th");
                int count = tds?.Count ?? 0;
                if (count > maxCells) { maxCells = count; headerRow = tr; }
            }
        }
        if (headerRow == null) return rows;

        var headers = new List<string>();
        var headerCells = headerRow.SelectNodes("th|td") ?? headerRow.ChildNodes;
        foreach (var th in headerCells)
            headers.Add(th.InnerText.Trim());

        var sel = string.IsNullOrWhiteSpace(rowSelector) ? ".//tbody/tr" : rowSelector;
        var dataRows = table.SelectNodes(sel);
        if (dataRows == null) return rows;

        var minCells = Math.Max(2, headers.Count / 2); // 至少要有表头一半的列才算数据行
        foreach (var tr in dataRows)
        {
            if (tr == headerRow) continue; // 跳过表头行
            var cells = tr.SelectNodes("td|th");
            if (cells == null || cells.Count < minCells) continue;
            var row = new Dictionary<string, string>();
            for (int i = 0; i < cells.Count && i < headers.Count; i++)
                row[headers[i]] = cells[i].InnerText.Trim();
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
                Encoding = GetString(exportEl, "encoding"),
                ViewStatePageUrl = GetString(exportEl, "viewStatePageUrl"),
                PythonScript = GetString(exportEl, "pythonScript"),
                QueryParams = ParseParams(exportEl),
                BusinessDataColumns = ParseStringArray(exportEl, "columns"),
                BusinessDataColumnMap = ParseIntMap(exportEl, "columnMap"),
                FieldMap = ParseStringMap(exportEl, "fieldMap"),
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

    private static List<string>? ParseStringArray(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var values = new List<string>();
        foreach (var item in arr.EnumerateArray())
            values.Add(item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : item.ToString());
        return values.Count > 0 ? values : null;
    }

    private static Dictionary<string, string>? ParseStringMap(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var obj) || obj.ValueKind != JsonValueKind.Object) return null;
        var map = new Dictionary<string, string>();
        foreach (var prop in obj.EnumerateObject())
            map[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() ?? "" : prop.Value.ToString();
        return map.Count > 0 ? map : null;
    }

    private static Dictionary<string, int>? ParseIntMap(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var obj) || obj.ValueKind != JsonValueKind.Object) return null;
        var map = new Dictionary<string, int>();
        foreach (var prop in obj.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var i)) map[prop.Name] = i;
            else if (prop.Value.ValueKind == JsonValueKind.String && int.TryParse(prop.Value.GetString(), out i)) map[prop.Name] = i;
        }
        return map.Count > 0 ? map : null;
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

    private static string? GetString(JsonElement el, string name)
    {
        // 大小写不敏感查找
        foreach (var prop in el.EnumerateObject())
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                return prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : null;
        return null;
    }

    // ═══════════════════════════════════════════
    // 登录与授权辅助
    // ═══════════════════════════════════════════

    private async Task<LoginResult> ExecuteLoginAsync(HttpClient httpClient, AuthConfig authConfig, CancellationToken ct)
    {
        _logger.LogInformation("登录配置: Path={P}, CaptchaUrl={C}, LoginPageUrl={L}, BodyJson={B}, TokenExtract={T}",
            authConfig.LoginPath, authConfig.CaptchaUrl ?? "null", authConfig.LoginPageUrl ?? "null",
            authConfig.LoginBodyJson != null ? "有" : "null", authConfig.TokenExtract != null ? "有" : "null");

        if (!string.IsNullOrWhiteSpace(authConfig.CaptchaUrl) && !string.IsNullOrWhiteSpace(authConfig.LoginPageUrl))
        {
            var captchaCookies = await CaptchaLoginAsync(httpClient, authConfig, ct);
            return new LoginResult { Cookies = captchaCookies };
        }

        var loginUrl = authConfig.LoginPath!.StartsWith('/') ? authConfig.LoginPath : "/" + authConfig.LoginPath;
        var method = (authConfig.LoginMethod ?? "POST").ToUpperInvariant();
        var maxAttempts = Math.Max(1, authConfig.LoginRetries ?? 3);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var loginVars = authConfig.PreLogin != null
                ? await ExecutePreLoginAsync(httpClient, authConfig.PreLogin, ct)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ApplyPasswordEncrypt(authConfig, loginVars);
            var finalLoginUrl = AppendQueryParams(loginUrl, ResolveTemplateParams(authConfig.LoginParams, loginVars));

            using var req = new HttpRequestMessage(method == "GET" ? HttpMethod.Get : HttpMethod.Post, finalLoginUrl);
            if (method != "GET")
            {
                if (!string.IsNullOrWhiteSpace(authConfig.LoginBodyJson))
                {
                    var bodyJson = ReplaceTemplateVars(authConfig.LoginBodyJson, loginVars);
                    var loginContentType = string.IsNullOrWhiteSpace(authConfig.LoginContentType) ? "application/json" : authConfig.LoginContentType!;
                    req.Content = new StringContent(bodyJson, Encoding.UTF8, loginContentType);
                }
                else
                {
                    var loginDict = new Dictionary<string, string>();
                    if (!string.IsNullOrWhiteSpace(authConfig.Username)) loginDict["username"] = ReplaceTemplateVars(authConfig.Username, loginVars);
                    if (!string.IsNullOrWhiteSpace(authConfig.Password)) loginDict["password"] = ReplaceTemplateVars(authConfig.Password, loginVars);
                    if (authConfig.ExtraFields != null)
                        foreach (var kvp in authConfig.ExtraFields) loginDict[kvp.Key] = ReplaceTemplateVars(kvp.Value, loginVars);
                    req.Content = new FormUrlEncodedContent(loginDict);
                }
            }

            ApplyRequestHeaders(req, authConfig.LoginHeaders);
            if (loginVars.TryGetValue("cookies", out var preLoginCookies) && !string.IsNullOrWhiteSpace(preLoginCookies))
                req.Headers.TryAddWithoutValidation("Cookie", preLoginCookies);
            var resp = await httpClient.SendAsync(req, ct);
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            var body = Encoding.UTF8.GetString(bytes);
            var cookies = MergeCookies(loginVars.TryGetValue("cookies", out var preLoginCookiesForResult) ? preLoginCookiesForResult : null, ExtractCookies(resp));

            _logger.LogInformation("登录 {Url} 第{Attempt}/{Max}次返回 {Status}, BodyBytes={BodyBytes}, Cookie={HasCookie}",
                loginUrl, attempt, maxAttempts, resp.StatusCode, bytes.Length, string.IsNullOrWhiteSpace(cookies) ? "无" : "有");

            if (!resp.IsSuccessStatusCode && resp.StatusCode != HttpStatusCode.Redirect)
                throw new InvalidOperationException($"Token已过期或未授权: 登录失败 HTTP {(int)resp.StatusCode}");

            if (resp.StatusCode != HttpStatusCode.Redirect)
                ThrowIfAuthFailureResponse(resp.Content.Headers.ContentType?.MediaType ?? "", bytes);

            var token = ExtractToken(body, authConfig.TokenExtract);
            if (!string.IsNullOrWhiteSpace(token))
            {
                _logger.LogInformation("登录响应已提取Token: {Token}", MaskToken(token));
                return new LoginResult { Cookies = cookies, Body = body, ExtractedToken = token };
            }

            if (authConfig.TokenExtract?.Required != true)
                return new LoginResult { Cookies = cookies, Body = body };

            if (attempt < maxAttempts)
                _logger.LogWarning("登录成功但未提取到Token，准备重试验证码登录");
        }

        throw new InvalidOperationException("Token已过期或未授权: 登录成功但未提取到Token");
    }

    private async Task<Dictionary<string, string>> ExecutePreLoginAsync(HttpClient httpClient, PreLoginConfig cfg, CancellationToken ct)
    {
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(cfg.Url)) return vars;

        var url = cfg.Url.StartsWith('/') ? cfg.Url : "/" + cfg.Url;
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyRequestHeaders(req, cfg.Headers);
        var resp = await httpClient.SendAsync(req, ct);
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        var body = Encoding.UTF8.GetString(bytes);
        var cookies = ExtractCookies(resp);
        if (!string.IsNullOrWhiteSpace(cookies)) vars["cookies"] = cookies!;

        _logger.LogInformation("预登录 {Url} 返回 {Status}, {Size} bytes", url, resp.StatusCode, bytes.Length);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Token已过期或未授权: 预登录失败 HTTP {(int)resp.StatusCode}");

        if (cfg.IncludeViewState)
        {
            AddRegexVar(vars, "__VIEWSTATE", body, "id=\\\"__VIEWSTATE\\\"[^>]*value=\\\"([^\\\"]*)\\\"|name=\\\"__VIEWSTATE\\\"[^>]*value=\\\"([^\\\"]*)\\\"");
            AddRegexVar(vars, "__VIEWSTATEGENERATOR", body, "id=\\\"__VIEWSTATEGENERATOR\\\"[^>]*value=\\\"([^\\\"]*)\\\"|name=\\\"__VIEWSTATEGENERATOR\\\"[^>]*value=\\\"([^\\\"]*)\\\"");
            AddRegexVar(vars, "__EVENTVALIDATION", body, "id=\\\"__EVENTVALIDATION\\\"[^>]*value=\\\"([^\\\"]*)\\\"|name=\\\"__EVENTVALIDATION\\\"[^>]*value=\\\"([^\\\"]*)\\\"");
        }

        if (cfg.Extract != null)
        {
            foreach (var item in cfg.Extract)
            {
                if (string.IsNullOrWhiteSpace(item.Name)) continue;
                string? value = null;
                if (string.Equals(item.Type, "json", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.Path))
                    value = ExtractJsonValue(body, item.Path!);
                else if (string.Equals(item.Type, "mathCaptcha", StringComparison.OrdinalIgnoreCase))
                {
                    var imgPath = string.IsNullOrWhiteSpace(item.Path) ? "img" : item.Path!;
                    var imgBase64 = ExtractJsonValue(body, imgPath);
                    value = SolveMathCaptcha(imgBase64);
                }
                else if (string.Equals(item.Type, "imageCaptcha", StringComparison.OrdinalIgnoreCase))
                {
                    value = RecognizeCaptchaText(bytes, item.CharWhitelist);
                }
                else if (string.Equals(item.Type, "regex", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.Pattern))
                {
                    var m = Regex.Match(body, item.Pattern!, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (m.Success) value = m.Groups.Count > 1 ? m.Groups[1].Value : m.Value;
                }
                else if (string.Equals(item.Type, "header", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(item.Path))
                {
                    if (resp.Headers.TryGetValues(item.Path!, out var values)) value = values.FirstOrDefault();
                }

                if (!string.IsNullOrWhiteSpace(value)) vars[item.Name] = value!;
                else if (item.Required) throw new InvalidOperationException($"Token已过期或未授权: 预登录未提取到 {item.Name}");
            }
        }

        return vars;
    }

    private static void ApplyPasswordEncrypt(AuthConfig authConfig, Dictionary<string, string> vars)
    {
        var cfg = authConfig.PasswordEncrypt;
        if (cfg == null || string.IsNullOrWhiteSpace(cfg.Name) || string.IsNullOrWhiteSpace(cfg.Value)) return;
        if (string.Equals(cfg.Type, "rsa", StringComparison.OrdinalIgnoreCase))
        {
            var encrypted = EncryptRsaPkcs1(cfg.Value!, cfg.PublicKey);
            if (!string.IsNullOrWhiteSpace(encrypted)) vars[cfg.Name!] = encrypted!;
        }
        else if (string.Equals(cfg.Type, "aes-cbc-zero", StringComparison.OrdinalIgnoreCase))
        {
            var encrypted = EncryptAesCbcZeroPadding(cfg.Value!, cfg.Key);
            if (!string.IsNullOrWhiteSpace(encrypted)) vars[cfg.Name!] = encrypted!;
        }
        else
        {
            vars[cfg.Name!] = cfg.Value!;
        }
    }

    private static string? EncryptRsaPkcs1(string plainText, string? publicKey)
    {
        if (string.IsNullOrWhiteSpace(publicKey)) return null;
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey.Replace("\n", "").Replace("\r", "")), out _);
            var encrypted = rsa.Encrypt(Encoding.UTF8.GetBytes(plainText), RSAEncryptionPadding.Pkcs1);
            return Convert.ToBase64String(encrypted);
        }
        catch { return null; }
    }

    private static void AddRegexVar(Dictionary<string, string> vars, string name, string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success) return;
        for (var i = 1; i < match.Groups.Count; i++)
        {
            if (!match.Groups[i].Success) continue;
            vars[name] = WebUtility.HtmlDecode(match.Groups[i].Value);
            return;
        }
    }

    private static string? EncryptAesCbcZeroPadding(string plainText, string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        try
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            using var aes = Aes.Create();
            aes.Key = keyBytes;
            aes.IV = keyBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.Zeros;
            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return Convert.ToBase64String(encrypted);
        }
        catch { return null; }
    }

    private static string? RecognizeCaptchaText(byte[] bytes, string? charWhitelist = null)
    {
        try
        {
            var tmpFile = Path.GetTempFileName() + ".jpg";
            File.WriteAllBytes(tmpFile, bytes);
            var whitelistArg = string.IsNullOrWhiteSpace(charWhitelist) ? "" : $", charsets=r'{charWhitelist}'";
            var psi = new System.Diagnostics.ProcessStartInfo("python", $"-c \"import ddddocr; o=ddddocr.DdddOcr(show_ad=False{whitelistArg}); print(o.classification(open(r'{tmpFile.Replace('\\', '/')}', 'rb').read()))\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var proc = System.Diagnostics.Process.Start(psi);
            var raw = proc!.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(15000);
            try { File.Delete(tmpFile); } catch { }
            return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        }
        catch { return null; }
    }

    private static string? SolveMathCaptcha(string? imageBase64)
    {
        if (string.IsNullOrWhiteSpace(imageBase64)) return null;
        try
        {
            var comma = imageBase64.IndexOf(',');
            if (comma >= 0) imageBase64 = imageBase64[(comma + 1)..];
            var bytes = Convert.FromBase64String(imageBase64);
            var tmpFile = Path.GetTempFileName() + ".jpg";
            File.WriteAllBytes(tmpFile, bytes);
            var psi = new System.Diagnostics.ProcessStartInfo("python", $"-c \"import re,sys; import ddddocr; o=ddddocr.DdddOcr(show_ad=False); s=o.classification(open(r'{tmpFile.Replace('\\', '/')}', 'rb').read()); print(s)\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var proc = System.Diagnostics.Process.Start(psi);
            var raw = proc!.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(15000);
            try { File.Delete(tmpFile); } catch { }
            return EvaluateCaptchaExpression(raw);
        }
        catch { return null; }
    }

    private static string? EvaluateCaptchaExpression(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var expr = raw.Replace("×", "*").Replace("x", "*").Replace("X", "*").Replace("÷", "/").Replace("=", "");
        expr = Regex.Replace(expr, @"\s+", "");

        // ddddocr 对该站点计算验证码通常能识别出前半段表达式，尾部可能混入干扰字符，
        // 如 0*1-7 实际应按 0*1 计算。因此只取第一个“数字 运算符 数字”。
        var match = Regex.Match(expr, @"(\d+)\s*([+\-*/])\s*(\d+)");
        if (!match.Success)
        {
            var num = Regex.Match(expr, @"\d+");
            return num.Success ? num.Value : null;
        }

        var a = int.Parse(match.Groups[1].Value);
        var bText = match.Groups[3].Value;
        // 计算型验证码一般是一位数运算；OCR 偶尔会把后面的干扰数字拼到第二个数，取首位更稳。
        var b = int.Parse(bText[..1]);
        var result = match.Groups[2].Value switch
        {
            "+" => a + b,
            "-" => a - b,
            "*" => a * b,
            "/" => b == 0 ? 0 : a / b,
            _ => 0
        };
        return result.ToString();
    }

    private static string ReplaceTemplateVars(string text, Dictionary<string, string> vars)
    {
        foreach (var kvp in vars)
        {
            text = text.Replace("{{" + kvp.Key + "}}", kvp.Value);
            text = text.Replace("{{urlencode:" + kvp.Key + "}}", Uri.EscapeDataString(kvp.Value));
        }
        return text;
    }

    private static string? ExtractJsonValue(string body, string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (TryGetByPath(doc.RootElement, path, out var el))
                return el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText();
        }
        catch { }
        return null;
    }

    private static void ApplyRequestHeaders(HttpRequestMessage req, Dictionary<string, string>? headers)
    {
        if (headers == null) return;
        foreach (var h in headers)
        {
            if (string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase) && req.Content != null)
            {
                req.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
                continue;
            }
            req.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }
    }

    private static Dictionary<string, string>? ResolveTemplateParams(Dictionary<string, string>? template, Dictionary<string, string> vars)
    {
        if (template == null || template.Count == 0) return null;
        var dict = new Dictionary<string, string>();
        foreach (var kvp in template) dict[kvp.Key] = ReplaceTemplateVars(kvp.Value, vars);
        return dict;
    }

    private static string AppendQueryParams(string url, Dictionary<string, string>? parameters)
    {
        if (parameters == null || parameters.Count == 0) return url;
        var qs = string.Join("&", parameters.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return url + (url.Contains('?') ? "&" : "?") + qs;
    }

    private static string? ExtractToken(string body, TokenExtractConfig? cfg)
    {
        if (cfg == null || string.IsNullOrWhiteSpace(body)) return null;
        if (!string.IsNullOrWhiteSpace(cfg.Source) && !string.Equals(cfg.Source, "json", StringComparison.OrdinalIgnoreCase))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var paths = new List<string>();
            if (cfg.Paths != null) paths.AddRange(cfg.Paths.Where(p => !string.IsNullOrWhiteSpace(p)));
            if (!string.IsNullOrWhiteSpace(cfg.Path)) paths.Add(cfg.Path!);
            if (paths.Count == 0) paths.AddRange(new[] { "token", "data.token", "access_token", "data.access_token" });

            foreach (var path in paths)
            {
                if (TryGetByPath(doc.RootElement, path, out var el))
                {
                    var token = el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText();
                    if (!string.IsNullOrWhiteSpace(token)) return token.Trim().Trim('"');
                }
            }
        }
        catch { }
        return null;
    }

    private static bool TryGetByPath(JsonElement root, string path, out JsonElement value)
    {
        value = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (value.ValueKind != JsonValueKind.Object) return false;
            var found = false;
            foreach (var prop in value.EnumerateObject())
            {
                if (string.Equals(prop.Name, segment, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    found = true;
                    break;
                }
            }
            if (!found) return false;
        }
        return true;
    }

    private static void ApplyTokenTargets(Dictionary<string, string> headers, string token, List<TokenTargetConfig>? targets)
    {
        if (targets == null || targets.Count == 0)
            targets = new List<TokenTargetConfig> { new() { Type = "header", Name = "Authorization", Prefix = "Bearer " } };

        foreach (var target in targets)
        {
            if (string.IsNullOrWhiteSpace(target.Name)) continue;
            if (string.Equals(target.Type, "cookie", StringComparison.OrdinalIgnoreCase))
            {
                var tokenCookie = $"{target.Name}={BuildTokenValue(token, target.Prefix)}";
                headers["Cookie"] = MergeCookies(GetHeaderValue(headers, "Cookie"), tokenCookie)!;
            }
            else
            {
                headers[target.Name] = BuildTokenValue(token, target.Prefix);
            }
        }
    }

    private static string BuildTokenValue(string token, string? prefix)
    {
        token = token.Trim();
        if (string.IsNullOrEmpty(prefix)) return token;
        return token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? token : prefix + token;
    }

    private static string? GetHeaderValue(Dictionary<string, string> headers, string name)
    {
        return headers.TryGetValue(name, out var value) ? value : null;
    }

    private static string MaskToken(string token)
    {
        if (token.Length <= 12) return $"***({token.Length})";
        return $"{token[..6]}...{token[^6..]}({token.Length})";
    }

    private static void ThrowIfAuthFailureResponse(string contentType, byte[] bytes)
    {
        if (bytes.Length == 0) return;
        var text = Encoding.UTF8.GetString(bytes);
        var lower = text.ToLowerInvariant();
        var keywords = new[] { "unauthorized", "未授权", "tokenversion expire", "token expired", "登录超时", "重新登录", "请登录", "未登录" };

        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (TryGetByPath(root, "code", out var codeEl) && IsAuthCode(codeEl))
                throw new InvalidOperationException($"Token已过期或未授权: code={GetElementText(codeEl)}");
            if (TryGetByPath(root, "status", out var statusEl) && IsAuthCode(statusEl))
                throw new InvalidOperationException($"Token已过期或未授权: status={GetElementText(statusEl)}");
            foreach (var path in new[] { "msg", "message", "error" })
            {
                if (TryGetByPath(root, path, out var msgEl))
                {
                    var msg = GetElementText(msgEl);
                    if (keywords.Any(k => msg.Contains(k, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException($"Token已过期或未授权: {msg}");
                }
            }
        }
        catch (JsonException) { }

        if (keywords.Any(k => lower.Contains(k.ToLowerInvariant())))
            throw new InvalidOperationException("Token已过期或未授权: Unauthorized response");
        if ((contentType.Contains("html", StringComparison.OrdinalIgnoreCase) || lower.Contains("<html")) &&
            (lower.Contains("login") || text.Contains("登录")))
            throw new InvalidOperationException("Token已过期或未授权: 返回登录页面");
    }

    private static bool IsAuthCode(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var code)) return code is 401 or 403;
        if (el.ValueKind == JsonValueKind.String) return el.GetString() is "401" or "403";
        return false;
    }

    private static string GetElementText(JsonElement el)
    {
        return el.ValueKind == JsonValueKind.String ? el.GetString() ?? "" : el.GetRawText();
    }

    // ═══════════════════════════════════════════
    // 内部模型
    // ═══════════════════════════════════════════

    private async Task<string?> CaptchaLoginAsync(HttpClient httpClient, AuthConfig authConfig, CancellationToken ct)
    {
        // 1. GET 登录页获取 ViewState + SessionId
        var pageUrl = authConfig.LoginPageUrl!.StartsWith('/') ? authConfig.LoginPageUrl : "/" + authConfig.LoginPageUrl;
        var pageResp = await httpClient.GetAsync(pageUrl, ct);
        var pageBytes = await pageResp.Content.ReadAsByteArrayAsync();
        var pageHtml = Encoding.GetEncoding("gb2312").GetString(pageBytes);
        var cookies1 = ExtractCookies(pageResp);
        _logger.LogInformation("获取登录页: {Status}, Cookie={Ck}", pageResp.StatusCode, cookies1);

        // 提取 ViewState
        var viewState = Regex.Match(pageHtml, "id=\"__VIEWSTATE\" value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        var viewStateGen = Regex.Match(pageHtml, "id=\"__VIEWSTATEGENERATOR\" value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        var eventVal = Regex.Match(pageHtml, "id=\"__EVENTVALIDATION\" value=\"([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        if (string.IsNullOrEmpty(viewState)) { _logger.LogWarning("未提取到ViewState"); return null; }

        // 2. GET 验证码图片
        var captchaUrl = authConfig.CaptchaUrl!.Replace("{time}", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
        var captchaResp = await httpClient.GetAsync(captchaUrl, ct);
        var captchaBytes = await captchaResp.Content.ReadAsByteArrayAsync();
        _logger.LogInformation("验证码图片: {Status}, {Size} bytes", captchaResp.StatusCode, captchaBytes.Length);

        // 3. OCR 识别（通过 Python 子进程）
        string code;
        try
        {
            var tmpFile = Path.GetTempFileName() + ".gif";
            await File.WriteAllBytesAsync(tmpFile, captchaBytes, ct);
            var psi = new System.Diagnostics.ProcessStartInfo("python", $"-c \"import sys; from PIL import Image; import pytesseract; img=Image.open('{tmpFile.Replace('\\', '/')}'); print(pytesseract.image_to_string(img, config='--psm 8 -c tessedit_char_whitelist=0123456789').strip())\"")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            var proc = System.Diagnostics.Process.Start(psi);
            code = (await proc!.StandardOutput.ReadToEndAsync()).Trim();
            proc.WaitForExit();
            File.Delete(tmpFile);
            _logger.LogInformation("OCR识别: {Code}", code);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "OCR失败: {Msg}", ex.Message); return null; }

        if (code.Length < 3) { _logger.LogWarning("OCR结果太短: {Code}", code); return null; }

        // 4. POST 登录
        var loginDict = new Dictionary<string, string>
        {
            ["__VIEWSTATE"] = viewState,
            ["__VIEWSTATEGENERATOR"] = viewStateGen,
            ["__EVENTVALIDATION"] = eventVal,
            ["tbaccount"] = authConfig.Username ?? "",
            ["tbpassword"] = authConfig.Password ?? "",
            ["tbvalid"] = code,
            ["cb_save"] = "on",
            ["imb_login.x"] = "50",
            ["imb_login.y"] = "10",
        };
        if (authConfig.ExtraFields != null)
            foreach (var kvp in authConfig.ExtraFields) loginDict[kvp.Key] = kvp.Value;

        var loginUrl = authConfig.LoginPath!.StartsWith('/') ? authConfig.LoginPath : "/" + authConfig.LoginPath;
        var loginResp = await httpClient.PostAsync(loginUrl, new FormUrlEncodedContent(loginDict), ct);
        var loginBody = await loginResp.Content.ReadAsStringAsync();
        var cookies2 = ExtractCookies(loginResp);
        var allCookies = string.IsNullOrEmpty(cookies1) ? cookies2 : (string.IsNullOrEmpty(cookies2) ? cookies1 : $"{cookies1}; {cookies2}");
        var loginRespBody = await loginResp.Content.ReadAsStringAsync();
        _logger.LogInformation("登录结果: {Status}, Location={Loc}, Body={Body}, Cookie={Ck}", loginResp.StatusCode,
            loginResp.Headers.Location?.ToString() ?? "none",
            loginRespBody?.Length > 300 ? loginRespBody[..300] : loginRespBody,
            allCookies);
        // 手动跟随 302 重定向以获取完整 session
        if (loginResp.StatusCode == HttpStatusCode.Redirect && loginResp.Headers.Location != null)
        {
            var redirUrl = loginResp.Headers.Location.ToString();
            if (!redirUrl.StartsWith("http")) redirUrl = redirUrl.StartsWith('/') ? redirUrl : "/" + redirUrl;
            var redirResp = await httpClient.GetAsync(redirUrl, ct);
            var redirCookies = ExtractCookies(redirResp);
            allCookies = MergeCookies(allCookies, redirCookies);
        }
        return allCookies;
    }

    private static string? ExtractCookies(HttpResponseMessage resp)
    {
        if (!resp.Headers.TryGetValues("Set-Cookie", out var cookies)) return null;
        var parts = new List<string>();
        foreach (var c in cookies)
        {
            var segs = c.Split(';');
            if (segs.Length > 0 && segs[0].Contains('='))
            {
                var kv = segs[0].Trim().Split('=', 2);
                if (kv.Length == 2) parts.Add($"{kv[0]}={kv[1]}");
            }
        }
        return parts.Count > 0 ? string.Join("; ", parts) : null;
    }

    private static string? MergeCookies(string? a, string? b)
    {
        if (a == null) return b;
        if (b == null) return a;
        // 后出现的 cookie 覆盖先出现的同名 cookie
        var dict = new Dictionary<string, string>();
        foreach (var c in (a + "; " + b).Split("; "))
        {
            var kv = c.Split('=', 2);
            if (kv.Length == 2) dict[kv[0].Trim()] = kv[1].Trim();
        }
        return string.Join("; ", dict.Select(kv => $"{kv.Key}={kv.Value}"));
    }

    private class AuthConfig
    {
        public string? LoginPath { get; set; }
        public string? LoginMethod { get; set; }
        public int? LoginRetries { get; set; }
        public string? LoginContentType { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? LoginBodyJson { get; set; }
        public string? CaptchaUrl { get; set; }
        public string? LoginPageUrl { get; set; }
        public Dictionary<string, string>? ExtraFields { get; set; }
        public Dictionary<string, string>? LoginParams { get; set; }
        public Dictionary<string, string>? LoginHeaders { get; set; }
        public PreLoginConfig? PreLogin { get; set; }
        public PasswordEncryptConfig? PasswordEncrypt { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
        public TokenExtractConfig? TokenExtract { get; set; }
        public TokenConfig? Token { get; set; }
    }

    private class PreLoginConfig
    {
        public string? Url { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
        public bool IncludeViewState { get; set; }
        public List<PreLoginExtractConfig>? Extract { get; set; }
    }

    private class PreLoginExtractConfig
    {
        public string? Name { get; set; }
        public string? Type { get; set; } = "json";
        public string? Path { get; set; }
        public string? Pattern { get; set; }
        public string? CharWhitelist { get; set; }
        public bool Required { get; set; }
    }

    private class PasswordEncryptConfig
    {
        public string? Type { get; set; } = "rsa";
        public string? Name { get; set; } = "password";
        public string? Value { get; set; }
        public string? PublicKey { get; set; }
        public string? Key { get; set; }
    }

    private class TokenExtractConfig
    {
        public string? Source { get; set; } = "json";
        public string? Path { get; set; }
        public List<string>? Paths { get; set; }
        public bool Required { get; set; }
        public List<TokenTargetConfig>? Targets { get; set; }
    }

    private class TokenTargetConfig
    {
        public string? Type { get; set; } = "header";
        public string? Name { get; set; }
        public string? Prefix { get; set; }
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
        public string? Encoding { get; set; }
        public string? BodyTemplate { get; set; }
        public string? ViewStatePageUrl { get; set; }
        public string? PythonScript { get; set; }
        public Dictionary<string, string>? QueryParams { get; set; }
        public PaginationConfig? Pagination { get; set; }
        public List<string>? BusinessDataColumns { get; set; }
        public Dictionary<string, int>? BusinessDataColumnMap { get; set; }
        public Dictionary<string, string>? FieldMap { get; set; }
    }

    private class PaginationConfig
    {
        public int Limit { get; set; } = 1000;
        public string OffsetField { get; set; } = "limitOffset";
        public string LimitField { get; set; } = "limit";
    }

    private class LoginResult
    {
        public string? Cookies { get; set; }
        public string? Body { get; set; }
        public string? ExtractedToken { get; set; }
    }

    /// <summary>执行 Python 脚本采集，读取 stdout JSON 数组</summary>
    private async Task<List<CollectionRowResult>> ExecutePythonScriptAsync(string scriptPath)
    {
        var results = new List<CollectionRowResult>();

        if (!System.IO.File.Exists(scriptPath))
        {
            _logger.LogError("Python 脚本不存在: {Path}", scriptPath);
            return results;
        }

        var psi = new System.Diagnostics.ProcessStartInfo("python", "\"" + scriptPath + "\"")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var proc = System.Diagnostics.Process.Start(psi);
        var output = await proc!.StandardOutput.ReadToEndAsync();
        proc.WaitForExit();

        if (proc.ExitCode != 0)
        {
            _logger.LogWarning("Python 脚本退出码 {Code}: {Out}",
                proc.ExitCode, output?.Length > 300 ? output[..300] : output);
        }

        try
        {
            var jsonRows = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(output);
            if (jsonRows != null)
            {
                foreach (var row in jsonRows)
                {
                    results.Add(new CollectionRowResult
                    {
                        RawDataJson = JsonSerializer.Serialize(row),
                        CleanedDataJson = JsonSerializer.Serialize(row),
                        TargetEntityType = "DistributionRecord",
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Python 结果 JSON 解析失败: {Out}",
                output?.Length > 200 ? output[..200] : output);
        }

        return results;
    }

    public class CollectionRowResult
    {
        public string RawDataJson { get; set; } = "{}";
        public string? CleanedDataJson { get; set; }
        public string? TargetEntityType { get; set; }
        public string? ErrorMessage { get; set; }
    }
}