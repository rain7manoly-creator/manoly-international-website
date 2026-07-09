using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Domain.Entities;

namespace ManolyWarehouse.Application.Interfaces;

public interface IActivityLogService
{
    /// <summary>
    /// Writes one audit entry for a warehouse change. Self-contained: resolves the
    /// current user's name + role and persists the row on its own. Call it only
    /// after the action's own SaveChanges has succeeded so the log reflects reality.
    /// </summary>
    Task RecordAsync(ActivityArea area, string action, CancellationToken ct = default);

    Task<PagedResult<ActivityLogViewModel>> ListAsync(
        string? areaFilter, int page, int pageSize, CancellationToken ct = default);
}

public interface IWarehouseGridService
{
    Task<WarehouseGridViewModel> GetGridAsync(CancellationToken ct = default);
}

public interface IShelfService
{
    Task<ShelfDetailViewModel?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<int> AddInventoryAsync(AddShelfInventoryRequest request, CancellationToken ct = default);
    Task AdjustInventoryAsync(int inventoryId, int bundleCount, int unitsPerBundle, string? notes, CancellationToken ct = default);
    Task DeleteInventoryAsync(int inventoryId, CancellationToken ct = default);
}

public interface IProductService
{
    Task<IReadOnlyList<ProductListItemViewModel>> ListAsync(CancellationToken ct = default);
    Task<PagedResult<ProductListItemViewModel>> ListPagedAsync(int page, int pageSize, string? search = null, CancellationToken ct = default);
    Task<ProductDetailViewModel?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<int> CreateAsync(string name, int categoryId, CancellationToken ct = default);
    Task UpdateAsync(int id, string name, int categoryId, CancellationToken ct = default);
    Task<ProductDeletionImpact> GetDeletionImpactAsync(int id, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryViewModel>> ListAsync(CancellationToken ct = default);
    Task<int> CreateAsync(string name, CancellationToken ct = default);
    Task UpdateAsync(int id, string name, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public interface IPurchaseOrderService
{
    Task<PagedResult<OrderListItemViewModel>> ListAsync(string? statusFilter, int page, int pageSize, CancellationToken ct = default);
    Task<OrderDetailViewModel?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<int> CreateAsync(CreateOrderRequest request, CancellationToken ct = default);
    Task AddItemAsync(int orderId, int productId, int bundleCount, int unitsPerBundle, CancellationToken ct = default);
    Task UpdateItemQuantityAsync(int itemId, int bundleCount, int unitsPerBundle, CancellationToken ct = default);
    Task DeleteItemAsync(int itemId, CancellationToken ct = default);
    Task AdvanceStatusAsync(int orderId, CancellationToken ct = default);
    Task CancelAsync(int orderId, CancellationToken ct = default);
    Task DeleteAsync(int orderId, CancellationToken ct = default);
    Task BulkDeleteAsync(IEnumerable<int> orderIds, CancellationToken ct = default);
    Task ReceiveItemToShelfAsync(int itemId, int shelfId, int position, CancellationToken ct = default);
    Task ReceiveItemToAreaZAsync(int itemId, CancellationToken ct = default);
}

public interface IAreaZService
{
    Task<PagedResult<AreaZItemViewModel>> ListActiveAsync(int page, int pageSize, CancellationToken ct = default);
    Task<AreaZItemViewModel?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<int> AddAsync(int productId, int bundleCount, int unitsPerBundle, string? notes, CancellationToken ct = default);
    Task UpdateAsync(int id, int bundleCount, int unitsPerBundle, string? notes, CancellationToken ct = default);
    Task DispatchAsync(int id, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task MoveToShelfAsync(int areaZId, int shelfId, int position, CancellationToken ct = default);
}

public interface IUserService
{
    Task<IReadOnlyList<UserViewModel>> ListAsync(CancellationToken ct = default);
    Task<UserViewModel?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<string> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task UpdateProfileAsync(string id, string fullName, bool isAdmin, CancellationToken ct = default);
    Task ResetPasswordAsync(string id, string newPassword, CancellationToken ct = default);
    Task ToggleActiveAsync(string id, CancellationToken ct = default);
}

public interface ISearchService
{
    Task<SearchResultsViewModel> SearchAsync(string query, CancellationToken ct = default);
}

public interface IInventoryPdfExporter
{
    Task<byte[]> GenerateInventorySnapshotAsync(CancellationToken ct = default);
}
