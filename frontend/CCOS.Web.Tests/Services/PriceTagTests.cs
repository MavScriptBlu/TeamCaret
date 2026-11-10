using CCOS.Web.Services;
using Xunit;

namespace CCOS.Web.Tests.Services;

/// <summary>Which prices a product shows, for the shared price display (CAR-78).</summary>
public class PriceTagTests
{
    [Fact]
    public void ForProduct_LowerMemberPrice_ShowsBothPrices()
    {
        var tag = PriceTag.ForProduct(35m, 28m);

        Assert.Equal("$35.00", tag.Regular);
        Assert.Equal("$28.00", tag.Member);
    }

    [Fact]
    public void ForProduct_NoMemberPrice_ShowsOnlyRegularPrice()
    {
        // Not a current member, so the API left the member price out (CAR-69).
        var tag = PriceTag.ForProduct(35m, null);

        Assert.Equal("$35.00", tag.Regular);
        Assert.Null(tag.Member);
    }

    [Fact]
    public void ForProduct_MemberPriceSameAsRegular_ShowsOnlyRegularPrice()
    {
        // No discount, so there's nothing to cross out.
        var tag = PriceTag.ForProduct(20m, 20m);

        Assert.Equal("$20.00", tag.Regular);
        Assert.Null(tag.Member);
    }

    [Fact]
    public void ForProduct_FreeForMembers_ShowsFree()
    {
        var tag = PriceTag.ForProduct(5m, 0m);

        Assert.Equal("$5.00", tag.Regular);
        Assert.Equal("Free", tag.Member);
    }

    [Fact]
    public void ForProduct_Large_IsKept()
    {
        Assert.True(PriceTag.ForProduct(10m, 8m, large: true).Large);
        Assert.False(PriceTag.ForProduct(10m, 8m).Large);
    }

    [Fact]
    public void IsEmpty_OnlyWhenNoPrices()
    {
        Assert.True(new PriceTag(null, null).IsEmpty);
        Assert.False(new PriceTag("$10.00", null).IsEmpty);
        Assert.False(new PriceTag(null, "$8.00").IsEmpty);
    }
}
