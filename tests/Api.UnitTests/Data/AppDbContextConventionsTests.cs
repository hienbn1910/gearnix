using Gearnix.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace Gearnix.Api.UnitTests.Data;

public sealed class AppDbContextConventionsTests
{
    [Fact]
    public void Model_SampleEntity_UsesSnakeCaseNames()
    {
        var entity = BuildSampleEntityModel();

        Assert.Equal("sample_order", entity.GetTableName());
        Assert.Equal("unit_price", Property(entity, nameof(SampleOrder.UnitPrice)).GetColumnName());
        Assert.Equal("created_at", Property(entity, nameof(SampleOrder.CreatedAt)).GetColumnName());
    }

    [Fact]
    public void Model_DecimalProperty_MapsToNumeric18And2()
    {
        var entity = BuildSampleEntityModel();

        Assert.Equal("numeric(18,2)", Property(entity, nameof(SampleOrder.UnitPrice)).GetColumnType());
    }

    [Fact]
    public void Model_DateTimeOffsetProperty_MapsToTimestamptz()
    {
        var entity = BuildSampleEntityModel();

        Assert.Equal("timestamp with time zone", Property(entity, nameof(SampleOrder.CreatedAt)).GetColumnType());
    }

    private static Microsoft.EntityFrameworkCore.Metadata.IEntityType BuildSampleEntityModel()
    {
        // The connection string is never opened: building the model does not connect.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=unused")
            .Options;
        using var context = new SampleDbContext(options);
        var entity = context.Model.FindEntityType(typeof(SampleOrder));
        Assert.NotNull(entity);
        return entity;
    }

    private static Microsoft.EntityFrameworkCore.Metadata.IProperty Property(
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity, string name)
    {
        var property = entity.FindProperty(name);
        Assert.NotNull(property);
        return property;
    }

    private sealed class SampleOrder
    {
        public Guid Id { get; set; }
        public decimal UnitPrice { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    // A test-only context: AppDbContext's conventions plus one sample entity.
    private sealed class SampleDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<SampleOrder>();
        }
    }
}
