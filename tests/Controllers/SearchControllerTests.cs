using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ManolyWarehouse.Application.Interfaces;
using ManolyWarehouse.Application.ViewModels;
using ManolyWarehouse.Controllers;
using Xunit;

namespace ManolyWarehouse.Tests.Controllers;

public class SearchControllerTests
{
    private readonly Mock<ISearchService> _searchSvc = new();
    private readonly SearchController _sut;

    public SearchControllerTests()
    {
        _sut = new SearchController(_searchSvc.Object);
        _sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
    }

    // ────────────────────────────────────────────────────────────────────────
    // Empty/null query → returns empty ViewModel without calling service
    // ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Index_EmptyQuery_ReturnsEmptyResultWithoutCallingService(string? q)
    {
        var result = await _sut.Index(q, default) as ViewResult;

        result.Should().NotBeNull();
        var vm = result!.Model.Should().BeOfType<SearchResultsViewModel>().Subject;
        vm.Query.Should().BeEmpty();
        _searchSvc.Verify(s => s.SearchAsync(It.IsAny<string>(), default), Times.Never);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Non-empty query → calls service and returns results
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Index_ValidQuery_CallsServiceAndReturnsResults()
    {
        var results = new SearchResultsViewModel
        {
            Query = "صنوبر",
            Products = new List<ProductSearchResult>
            {
                new() { ProductId = 1, Name = "خشب صنوبر", CategoryName = "أبواب", LocationCount = 2 }
            }
        };
        _searchSvc.Setup(s => s.SearchAsync("صنوبر", default)).ReturnsAsync(results);

        var result = await _sut.Index("صنوبر", default) as ViewResult;

        result!.Model.Should().Be(results);
        _searchSvc.Verify(s => s.SearchAsync("صنوبر", default), Times.Once);
    }
}
