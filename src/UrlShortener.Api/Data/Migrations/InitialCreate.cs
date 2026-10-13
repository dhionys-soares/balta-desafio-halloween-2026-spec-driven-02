using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UrlShortener.Api.Data;

namespace UrlShortener.Api.Data.Migrations;

[DbContext(typeof(LinkDbContext))]
[Migration("20261013000000_InitialCreate")]
public sealed class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Links",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                TargetUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                AccessCount = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                ManagementKeyHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Links", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_Links_Code",
            table: "Links",
            column: "Code",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "Links");

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.0");

        modelBuilder.Entity("UrlShortener.Api.Domain.Link", entity =>
        {
            entity.Property<Guid>("Id").HasColumnType("TEXT");
            entity.Property<long>("AccessCount")
                .ValueGeneratedOnAdd()
                .HasColumnType("INTEGER")
                .HasDefaultValue(0L);
            entity.Property<string>("Code").IsRequired().HasMaxLength(20).HasColumnType("TEXT");
            entity.Property<DateTime>("CreatedAtUtc").HasColumnType("TEXT");
            entity.Property<DateTime?>("ExpiresAtUtc").HasColumnType("TEXT");
            entity.Property<string>("ManagementKeyHash")
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnType("TEXT");
            entity.Property<string>("TargetUrl").IsRequired().HasMaxLength(2048).HasColumnType("TEXT");
            entity.HasKey("Id");
            entity.HasIndex("Code").IsUnique();
            entity.ToTable("Links");
        });
    }
}
