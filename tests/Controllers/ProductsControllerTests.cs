using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Controllers;
using Xunit;

namespace ManolyWarehouse.Tests.Controllers;

public class ProductsControllerTests
{
    private readonly Mock<IProductService>  _productSvc  = new();
    private readonly Mock<ICategoryService> _categorySvc = new();
    private readonly ProductsController _sut;

    public ProductsControllerTests()
    {
        _sut = new ProductsController(_productSvc.Object, _categorySvc.Object);
        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        _categorySvc.Setup(c => c.ListAsync(default))
                    .ReturnsAsync(new List<CategoryViewModel>());
    }

    // ────────────────────────────────────────────────────────────────────────
    // Index — returns view with product list
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Index_ReturnsViewWithProductList()
    {
        var products = new List<ProductListItemViewModel>
        {
            new() { Id = 1, Name = "خشب صنوبر", CategoryName = "أبواب" }
        };
        _productSvc.Setup(p => p.ListAsync(default)).ReturnsAsync(products);

        var result = await _sut.Index(default) as ViewResult;

        result!.Model.Should().BeEquivalentTo(products);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Detail — unknown id returns 404
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Detail_UnknownId_Returns404()
    {
        _productSvc.Setup(p => p.GetByIdAsync(999, default)).ReturnsAsync((ProductDetailViewModel?)null);

        var result = await _sut.Detail(999, default);

        result.Should().BeOfType<NotFoundResult>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Add POST — blank name blocked at controller (PROD-01 guard)
    // ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Add_POST_BlankName_ReturnsViewWithModelError(string name)
    {
        var result = await _sut.Add(name, 1, default);

        result.Should().BeOfType<ViewResult>();
        _sut.ModelState.IsValid.Should().BeFalse();
        _productSvc.Verify(p => p.CreateAsync(It.IsAny<string>(), It.IsAny<int>(), default), Times.Never);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Add POST — valid name creates product and redirects to Index
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Add_POST_ValidName_CreatesAndRedirectsToIndex()
    {
        _productSvc.Setup(p => p.CreateAsync("خشب جوز", 2, default)).ReturnsAsync(10);

        var result = await _sut.Add("خشب جوز", 2, default) as RedirectToActionResult;

        result!.ActionName.Should().Be("Index");
        _productSvc.Verify(p => p.CreateAsync("خشب جوز", 2, default), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Edit GET — unknown product returns 404
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_GET_UnknownProduct_Returns404()
    {
        _productSvc.Setup(p => p.GetByIdAsync(999, default)).ReturnsAsync((ProductDetailViewModel?)null);

        var result = await _sut.Edit(999, default);

        result.Should().BeOfType<NotFoundResult>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Edit POST — updates and redirects to Detail
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_POST_ValidInput_UpdatesAndRedirectsToDetail()
    {
        _productSvc.Setup(p => p.UpdateAsync(5, "خشب بلوط", 3, default)).Returns(Task.CompletedTask);

        var result = await _sut.Edit(5, "خشب بلوط", 3, default) as RedirectToActionResult;

        result!.ActionName.Should().Be("Detail");
        result.RouteValues!["id"].Should().Be(5);
        _productSvc.Verify(p => p.UpdateAsync(5, "خشب بلوط", 3, default), Times.Once);
    }

    // ────────────────────────────────────────────────────────────────────────
    // ConfirmDelete GET — unknown product returns 404
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmDelete_GET_UnknownProduct_Returns404()
    {
        _productSvc.Setup(p => p.GetByIdAsync(999, default)).ReturnsAsync((ProductDetailViewModel?)null);

        var result = await _sut.ConfirmDelete(999, default);

        result.Should().BeOfType<NotFoundResult>();
    }

    // ────────────────────────────────────────────────────────────────────────
    // ConfirmDelete GET — sets impact in ViewBag (PROD-03 warning)
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmDelete_GET_SetsImpactInViewBag()
    {
        var product = new ProductDetailViewModel { Id = 1, Name = "خشب صنوبر", CategoryName = "أبواب" };
        var impact  = new ProductDeletionImpact { ShelfInventoryCount = 3, AreaZCount = 1 };

        _productSvc.Setup(p => p.GetByIdAsync(1, default)).ReturnsAsync(product);
        _productSvc.Setup(p => p.GetDeletionImpactAsync(1, default)).ReturnsAsync(impact);

        var result = await _sut.ConfirmDelete(1, default) as ViewResult;

        result!.ViewData["Impact"].Should().Be(impact);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Delete POST — calls service and redirects to Index
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_POST_CallsServiceAndRedirectsToIndex()
    {
        _productSvc.Setup(p => p.DeleteAsync(1, default)).Returns(Task.CompletedTask);

        var result = await _sut.Delete(1, default) as RedirectToActionResult;

        result!.ActionName.Should().Be("Index");
        _productSvc.Verify(p => p.DeleteAsync(1, default), Times.Once);
    }
}
