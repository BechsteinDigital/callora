using Microsoft.EntityFrameworkCore;

namespace Callora.Plugin.Composer.Infrastructure.Persistence.Migrations;

/// <summary>
/// Das Modell, wie die Migrationen es sehen — einmal geschrieben, von Snapshot und Designer
/// benutzt.
/// <para>
/// Auto-generierte EF-Migrationen wiederholen dieses Modell in jeder Designer-Datei. Hier wird es
/// von Hand gepflegt (das Tooling kann den Plugin-Kontext nicht bauen), und zweimal von Hand
/// gepflegt hieße: zweimal die Chance, es unterschiedlich zu tun.
/// </para>
/// </summary>
internal static class ComposerModelDefinition
{
    public static void Build(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("plugin_composer")
                .HasAnnotation("ProductVersion", "10.0.9")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            modelBuilder.Entity("Callora.Plugin.Composer.Domain.SurfaceLayout", b =>
                {
                    b.Property<string>("Key")
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)")
                        .HasColumnName("key");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)")
                        .HasColumnName("name");

                    b.Property<string>("SurfaceKey")
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)")
                        .HasColumnName("surface_key");

                    b.Property<string>("WorkspaceKey")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)")
                        .HasColumnName("workspace_key");

                    b.HasKey("Key");

                    b.HasIndex("WorkspaceKey", "SurfaceKey");

                    b.ToTable("surface_layouts", "plugin_composer");
                });

            modelBuilder.Entity("Callora.Plugin.Composer.Domain.SurfaceLayoutVersion", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<DateTimeOffset>("ChangedAtUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("changed_at_utc");

                    b.Property<DateTimeOffset>("CreatedAtUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at_utc");

                    b.Property<string>("CreatedBy")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)")
                        .HasColumnName("created_by");

                    b.Property<string>("Document")
                        .IsRequired()
                        .HasColumnType("jsonb")
                        .HasColumnName("document");

                    b.Property<string>("Label")
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)")
                        .HasColumnName("label");

                    b.Property<string>("LayoutKey")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)")
                        .HasColumnName("layout_key");

                    b.Property<DateTimeOffset?>("PublishedAtUtc")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("published_at_utc");

                    b.Property<string>("PublishedBy")
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)")
                        .HasColumnName("published_by");

                    b.Property<string>("State")
                        .IsRequired()
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)")
                        .HasColumnName("state");

                    b.Property<int>("VersionNumber")
                        .HasColumnType("integer")
                        .HasColumnName("version_number");

                    b.HasKey("Id");

                    b.HasIndex("LayoutKey", "State");

                    b.HasIndex("LayoutKey", "VersionNumber")
                        .IsUnique();

                    b.ToTable("surface_layout_versions", "plugin_composer");
                });
#pragma warning restore 612, 618
    }
}
