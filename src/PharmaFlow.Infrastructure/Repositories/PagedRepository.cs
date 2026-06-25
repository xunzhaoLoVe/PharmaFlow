using Microsoft.EntityFrameworkCore;
using PharmaFlow.Domain.Interfaces;
using System.Linq.Expressions;

namespace PharmaFlow.Infrastructure.Repositories;

public class PagedRepository<T> : Repository<T>, IPagedRepository<T> where T : class
{
    public PagedRepository(DbContext context) : base(context) { }

    public async Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(
        int pageIndex, int pageSize,
        Expression<Func<T, bool>>? filter = null,
        Expression<Func<T, object>>? orderBy = null,
        bool descending = false)
    {
        IQueryable<T> query = _dbSet;

        if (filter != null)
            query = query.Where(filter);

        var totalCount = await query.CountAsync();

        if (orderBy != null)
        {
            query = descending ? query.OrderByDescending(orderBy) : query.OrderBy(orderBy);
        }

        var items = await query
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
