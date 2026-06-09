using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;

namespace ManolyWarehouse.Application.Services;

// These stubs allow the project to build and run end-to-end so we can
// implement and test services one at a time in the next phase.
// Each method throws NotImplementedException until replaced.

internal sealed class StubException : NotImplementedException
{
    public StubException(string service)
        : base($"{service} is not implemented yet. This will be built in the service implementation phase.") { }
}

public class WarehouseGridServiceStub : IWarehouseGridService
{
    public Task<WarehouseGridViewModel> GetGridAsync(CancellationToken ct = default) =>
        Task.FromResult(new WarehouseGridViewModel());
}

public class ShelfServiceStub : IShelfService
{
    public Task<ShelfDetailViewModel?> GetByCodeAsync(string code, CancellationToken ct = default)
        => Task.FromResult<ShelfDetailViewModel?>(null);
    public Task<int> AddInventoryAsync(AddShelfInventoryRequest request, CancellationToken ct = default)
        => throw new StubException(nameof(IShelfService));
    public Task AdjustInventoryAsync(int inventoryId, int bundleCount, int unitsPerBundle, string? notes, CancellationToken ct = default)
        => throw new StubException(nameof(IShelfService));
    public Task DeleteInventoryAsync(int inventoryId, CancellationToken ct = default)
        => throw new StubException(nameof(IShelfService));
}

public class ProductServiceStub : IProductService
{
    public Task<IReadOnlyList<ProductListItemViewModel>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ProductListItemViewModel>>(Array.Empty<ProductListItemViewModel>());
    public Task<PagedResult<ProductListItemViewModel>> ListPagedAsync(int page, int pageSize, string? search = null, CancellationToken ct = default)
        => Task.FromResult(new PagedResult<ProductListItemViewModel> { Page = page, PageSize = pageSize });
    public Task<ProductDetailViewModel?> GetByIdAsync(int id, CancellationToken ct = default)
        => Task.FromResult<ProductDetailViewModel?>(null);
    public Task<int> CreateAsync(string name, int categoryId, CancellationToken ct = default)
        => throw new StubException(nameof(IProductService));
    public Task UpdateAsync(int id, string name, int categoryId, CancellationToken ct = default)
        => throw new StubException(nameof(IProductService));
    public Task<ProductDeletionImpact> GetDeletionImpactAsync(int id, CancellationToken ct = default)
        => Task.FromResult(new ProductDeletionImpact());
    public Task DeleteAsync(int id, CancellationToken ct = default)
        => throw new StubException(nameof(IProductService));
}

public class CategoryServiceStub : ICategoryService
{
    public Task<IReadOnlyList<CategoryViewModel>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CategoryViewModel>>(Array.Empty<CategoryViewModel>());
    public Task<int> CreateAsync(string name, CancellationToken ct = default)
        => throw new StubException(nameof(ICategoryService));
    public Task UpdateAsync(int id, string name, CancellationToken ct = default)
        => throw new StubException(nameof(ICategoryService));
    public Task DeleteAsync(int id, CancellationToken ct = default)
        => throw new StubException(nameof(ICategoryService));
}

public class PurchaseOrderServiceStub : IPurchaseOrderService
{
    public Task<PagedResult<OrderListItemViewModel>> ListAsync(string? statusFilter, int page, int pageSize, CancellationToken ct = default)
        => Task.FromResult(new PagedResult<OrderListItemViewModel> { Page = page, PageSize = pageSize });
    public Task<OrderDetailViewModel?> GetByIdAsync(int id, CancellationToken ct = default)
        => Task.FromResult<OrderDetailViewModel?>(null);
    public Task<int> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)
        => throw new StubException(nameof(IPurchaseOrderService));
    public Task AddItemAsync(int orderId, int productId, int bundleCount, int unitsPerBundle, CancellationToken ct = default)
        => throw new StubException(nameof(IPurchaseOrderService));
    public Task UpdateItemQuantityAsync(int itemId, int bundleCount, int unitsPerBundle, CancellationToken ct = default)
        => throw new StubException(nameof(IPurchaseOrderService));
    public Task DeleteItemAsync(int itemId, CancellationToken ct = default)
        => throw new StubException(nameof(IPurchaseOrderService));
    public Task AdvanceStatusAsync(int orderId, CancellationToken ct = default)
        => throw new StubException(nameof(IPurchaseOrderService));
    public Task CancelAsync(int orderId, CancellationToken ct = default)
        => throw new StubException(nameof(IPurchaseOrderService));
    public Task DeleteAsync(int orderId, CancellationToken ct = default)
        => throw new StubException(nameof(IPurchaseOrderService));
    public Task BulkDeleteAsync(IEnumerable<int> orderIds, CancellationToken ct = default)
        => throw new StubException(nameof(IPurchaseOrderService));
    public Task ReceiveItemToShelfAsync(int itemId, int shelfId, int position, CancellationToken ct = default)
        => throw new StubException(nameof(IPurchaseOrderService));
    public Task ReceiveItemToAreaZAsync(int itemId, CancellationToken ct = default)
        => throw new StubException(nameof(IPurchaseOrderService));
}

public class AreaZServiceStub : IAreaZService
{
    public Task<PagedResult<AreaZItemViewModel>> ListActiveAsync(int page, int pageSize, CancellationToken ct = default)
        => Task.FromResult(new PagedResult<AreaZItemViewModel> { Page = page, PageSize = pageSize });
    public Task<AreaZItemViewModel?> GetByIdAsync(int id, CancellationToken ct = default)
        => Task.FromResult<AreaZItemViewModel?>(null);
    public Task<int> AddAsync(int productId, int bundleCount, int unitsPerBundle, string? notes, CancellationToken ct = default)
        => throw new StubException(nameof(IAreaZService));
    public Task UpdateAsync(int id, int bundleCount, int unitsPerBundle, string? notes, CancellationToken ct = default)
        => throw new StubException(nameof(IAreaZService));
    public Task DispatchAsync(int id, CancellationToken ct = default)
        => throw new StubException(nameof(IAreaZService));
    public Task DeleteAsync(int id, CancellationToken ct = default)
        => throw new StubException(nameof(IAreaZService));
    public Task MoveToShelfAsync(int areaZId, int shelfId, int position, CancellationToken ct = default)
        => throw new StubException(nameof(IAreaZService));
}

public class UserServiceStub : IUserService
{
    public Task<IReadOnlyList<UserViewModel>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<UserViewModel>>(Array.Empty<UserViewModel>());
    public Task<UserViewModel?> GetByIdAsync(string id, CancellationToken ct = default)
        => Task.FromResult<UserViewModel?>(null);
    public Task<string> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
        => throw new StubException(nameof(IUserService));
    public Task UpdateProfileAsync(string id, string fullName, bool isAdmin, CancellationToken ct = default)
        => throw new StubException(nameof(IUserService));
    public Task ResetPasswordAsync(string id, string newPassword, CancellationToken ct = default)
        => throw new StubException(nameof(IUserService));
    public Task ToggleActiveAsync(string id, CancellationToken ct = default)
        => throw new StubException(nameof(IUserService));
}

public class SearchServiceStub : ISearchService
{
    public Task<SearchResultsViewModel> SearchAsync(string query, CancellationToken ct = default)
        => Task.FromResult(new SearchResultsViewModel { Query = query });
}

public class InventoryPdfExporterStub : IInventoryPdfExporter
{
    public Task<byte[]> GenerateInventorySnapshotAsync(CancellationToken ct = default)
        => throw new StubException(nameof(IInventoryPdfExporter));
}
