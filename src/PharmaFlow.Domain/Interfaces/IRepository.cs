using System.Linq.Expressions;

namespace PharmaFlow.Domain.Interfaces;

/// <summary>通用仓储接口</summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<T>> GetAllAsync();
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
}

/// <summary>带分页的仓储接口</summary>
public interface IPagedRepository<T> : IRepository<T> where T : class
{
    Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(
        int pageIndex, int pageSize,
        Expression<Func<T, bool>>? filter = null,
        Expression<Func<T, object>>? orderBy = null,
        bool descending = false);
}
