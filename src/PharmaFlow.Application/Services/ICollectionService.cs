using PharmaFlow.Application.DTOs;

namespace PharmaFlow.Application.Services;

public interface ICollectionService
{
    // Source CRUD
    Task<PagedResult<CollectionSourceDto>> GetSourcesPagedAsync(int pageIndex, int pageSize, string? search = null);
    Task<CollectionSourceDto?> GetSourceByIdAsync(Guid id);
    Task<CollectionSourceDto> CreateSourceAsync(CollectionSourceDto dto);
    Task UpdateSourceAsync(CollectionSourceDto dto);
    Task DeleteSourceAsync(Guid id);
    Task<IReadOnlyList<DropdownItem>> GetSourceDropdownAsync();

    // Task management
    Task<PagedResult<CollectionTaskDto>> GetTasksPagedAsync(int pageIndex, int pageSize, Guid? sourceId = null);
    Task<CollectionTaskDto?> GetTaskByIdAsync(Guid id);
    Task DeleteTaskAsync(Guid id);
    Task SubmitTaskAsync(Guid id);
    Task UnsubmitTaskAsync(Guid id);
    Task ClearSummaryAsync();
    Task<IReadOnlyList<DropdownItem>> GetSummarySourceDropdownAsync();
    Task<CollectionSummaryFilterOptionsDto> GetSummaryFilterOptionsAsync(Guid? sourceId = null);
    Task<PagedResult<CollectionSummaryRecordDto>> GetSummaryPagedAsync(int pageIndex, int pageSize,
        Guid? sourceId = null, string? search = null, string? dataType = null,
        DateTime? dateFrom = null, DateTime? dateTo = null, string? syncStatus = null,
        string? sortField = null, string? sortOrder = null,
        string? sourceSearch = null, string? targetSearch = null,
        string? drugName = null, string? specification = null,
        string? batchNo = null, string? province = null);
    Task<SyncSummaryResultDto> SyncSummaryAsync(Guid? sourceId = null);
    Task<ReapplyTransformResultDto> ReapplyTransformAsync(Guid sourceId);

    // Manual trigger
    Task<CollectionTaskDto> TriggerManualAsync(Guid sourceId, Guid? triggeredByUserId = null);

    // Records and review
    Task<PagedResult<CollectionRecordDto>> GetRecordsPagedAsync(int pageIndex, int pageSize,
        Guid? taskId = null, Guid? sourceId = null, string? status = null);
    Task ApproveRecordsAsync(List<Guid> recordIds, Guid reviewerId);
    Task RejectRecordsAsync(List<Guid> recordIds, Guid reviewerId, string reason);
    Task<int> ImportApprovedAsync(List<Guid> recordIds);
}
