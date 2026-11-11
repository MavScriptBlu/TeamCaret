using System.Linq;
using CCOS.Web.Services;
using Xunit;

namespace CCOS.Web.Tests.Services;

/// <summary>Menu links and which one is highlighted as the current page (CAR-64).</summary>
public class SiteNavTests
{
    [Theory]
    [InlineData("/Products", "Merch")]
    [InlineData("/Products/Index", "Merch")]
    [InlineData("/Products/Details/3", "Merch")]
    [InlineData("/products/search", "Merch")]
    [InlineData("/Events", "Events")]
    [InlineData("/Events/Details/2", "Events")]
    [InlineData("/Officer/Products", "Product dashboard")]
    [InlineData("/Officer/Products/Edit/5", "Product dashboard")]
    [InlineData("/Officer/Events/Create", "Create event")]
    [InlineData("/Officer/Events/Index", "Manage events")]
    [InlineData("/Officer/Events/Edit/2", "Manage events")]
    [InlineData("/Officer/Venues/Create", "Add venue")]
    [InlineData("/Officer/Venues/", "Manage venues")]
    [InlineData("/Officer/Venues/Edit/3", "Manage venues")]
    public void CurrentFor_MenuPage_ReturnsItsLink(string path, string expectedText)
    {
        Assert.Equal(expectedText, SiteNav.CurrentFor(path)?.Text);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Account/Login")]
    [InlineData("/EventsArchive")]
    [InlineData(null)]
    public void CurrentFor_PageNotInMenu_ReturnsNull(string? path)
    {
        Assert.Null(SiteNav.CurrentFor(path));
    }

    [Fact]
    public void Links_OfficerLinksAreOfficerOnlyAndGrouped()
    {
        Assert.All(
            SiteNav.Links.Where(link => link.Href.StartsWith("/Officer/", System.StringComparison.Ordinal)),
            link =>
            {
                Assert.True(link.OfficerOnly);
                Assert.Equal(SiteNav.OfficerSection, link.Section);
            });

        Assert.All(
            SiteNav.Links.Where(link => !link.OfficerOnly),
            link => Assert.Equal(SiteNav.ShopSection, link.Section));
    }
}
