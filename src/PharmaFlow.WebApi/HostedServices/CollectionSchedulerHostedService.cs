using Cronos;
using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.Services;
using PharmaFlow.Domain.Enums;
using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.WebApi.HostedServices;

/// <summary>采集定时调度服务 — 轮询 Cron 表达式，自动触发到期采集任务</summary>
public class CollectionSchedulerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CollectionSchedulerHostedService> _logger;

    // 防止同一采集源并发执行
    private static readonly HashSet<Guid> RunningSources = new();
    private static readonly object LockObj = new();

    public CollectionSchedulerHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<CollectionSchedulerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("采集调度服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "采集调度 Tick 异常");
            }

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        // 查找启用且配置了 Cron 的采集源
        var sources = await db.CollectionSources
            .Where(s => !s.IsDeleted && s.IsEnabled && s.ScheduleCron != null)
            .ToListAsync(cancellationToken);

        foreach (var source in sources)
        {
            if (string.IsNullOrWhiteSpace(source.ScheduleCron)) continue;

            // 解析 Cron 表达式
            CronExpression cronExpression;
            try
            {
                cronExpression = CronExpression.Parse(source.ScheduleCron, CronFormat.Standard);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "采集源 {SourceName} 的 Cron 表达式无效: {Cron}", source.Name, source.ScheduleCron);
                continue;
            }

            // 获取最近一次应该执行的时间
            var lastRun = source.LastCollectionAt ?? source.CreatedAt;
            var nextOccurrence = cronExpression.GetNextOccurrence(lastRun, TimeZoneInfo.Utc, inclusive: false);

            // 检查是否到了执行时间，且当前没有在执行
            if (nextOccurrence.HasValue && nextOccurrence.Value <= now)
            {
                lock (LockObj)
                {
                    if (!RunningSources.Add(source.Id))
                    {
                        _logger.LogDebug("采集源 {SourceName} 正在执行中，跳过本次调度", source.Name);
                        continue;
                    }
                }

                _logger.LogInformation("定时触发采集: {SourceName} (Cron: {Cron})", source.Name, source.ScheduleCron);

                // 异步执行，不阻塞轮询循环
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var execScope = _scopeFactory.CreateScope();
                        var collectionService = execScope.ServiceProvider.GetRequiredService<ICollectionService>();
                        await collectionService.TriggerManualAsync(source.Id, null);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "定时采集 {SourceName} 执行失败", source.Name);
                    }
                    finally
                    {
                        lock (LockObj) { RunningSources.Remove(source.Id); }
                    }
                }, cancellationToken);
            }
        }
    }
}
