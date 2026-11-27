using Gearnix.Api.Modules.Catalog;

namespace Gearnix.Api.UnitTests.Catalog;

public sealed class VietnameseTextTests
{
    [Theory]
    [InlineData("Điện thoại", "dien thoai")]
    [InlineData("ĐỒNG HỒ thông minh", "dong ho thong minh")]
    [InlineData("Tai nghe không dây", "tai nghe khong day")]
    [InlineData("Màn hình 27\" 4K", "man hinh 27\" 4k")]
    [InlineData("  Laptop   Gaming  ", "laptop gaming")]
    [InlineData("Bàn phím cơ", "ban phim co")]
    public void Normalize_VietnameseText_LowercaseWithoutDiacritics(string input, string expected)
    {
        Assert.Equal(expected, VietnameseText.Normalize(input));
    }

    [Theory]
    [InlineData("Điện thoại Nova X 5G", "dien-thoai-nova-x-5g")]
    [InlineData("Linh kiện PC", "linh-kien-pc")]
    [InlineData("Màn hình 27\" 4K (IPS)", "man-hinh-27-4k-ips")]
    [InlineData("--Sạc & cáp--", "sac-cap")]
    [InlineData("Ổ cứng SSD 1TB / NVMe", "o-cung-ssd-1tb-nvme")]
    public void ToSlug_Name_UrlFriendlyAsciiWithSingleHyphens(string input, string expected)
    {
        Assert.Equal(expected, VietnameseText.ToSlug(input));
    }

    [Fact]
    public void ToSlug_NoLettersOrDigits_ReturnsFallback()
    {
        Assert.Equal("item", VietnameseText.ToSlug("!!! ???"));
    }
}
