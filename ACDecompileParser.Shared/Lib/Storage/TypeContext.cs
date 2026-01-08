using Microsoft.EntityFrameworkCore;
using ACDecompileParser.Shared.Lib.Models;

namespace ACDecompileParser.Shared.Lib.Storage;

public class TypeContext : DbContext
{
    public TypeContext(DbContextOptions<TypeContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Suppress the navigation include warning that occurs with complex navigation properties
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.NavigationBaseIncludeIgnored));

        base.OnConfiguring(optionsBuilder);
    }

    public DbSet<TypeModel> Types { get; set; }
    public DbSet<TypeReference> TypeReferences { get; set; }
    public DbSet<EnumMemberModel> EnumMembers { get; set; }
    public DbSet<StructMemberModel> StructMembers { get; set; }
    public DbSet<FunctionParamModel> FunctionParameters { get; set; }
    public DbSet<FunctionSignatureModel> FunctionSignatures { get; set; }
    public DbSet<TypeTemplateArgument> TypeTemplateArguments { get; set; }
    public DbSet<TypeInheritance> TypeInheritances { get; set; }
    public DbSet<TypeDefModel> TypeDefs { get; set; }
    public DbSet<FunctionBodyModel> FunctionBodies { get; set; }
    public DbSet<StaticVariableModel> StaticVariables { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // TypeModel configuration
        modelBuilder.Entity<TypeModel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BaseName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Namespace).HasMaxLength(255);
            entity.Property(e => e.Source).HasColumnType("TEXT");
            entity.Property(e => e.StoredFullyQualifiedName).HasColumnName("FullyQualifiedName").IsRequired()
                .HasMaxLength(500);
            entity.Property(e => e.IsIgnored).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.File).HasMaxLength(500);
            entity.Property(e => e.LineNumber);
            entity.HasIndex(e => new { e.Namespace, e.BaseName });
            entity.HasIndex(e => e.IsIgnored);
            entity.Property(e => e.IsBitmask).IsRequired().HasDefaultValue(false);

            // Ignore the computed FullyQualifiedName property to avoid EF Core confusion
            entity.Ignore(e => e.FullyQualifiedName);
        });

        // TypeReference configuration
        modelBuilder.Entity<TypeReference>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TypeString).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FullyQualifiedType).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => e.FullyQualifiedType);
            entity.HasIndex(e => e.TypeString);
            entity.Property(e => e.File).HasMaxLength(500);
            entity.Property(e => e.LineNumber);
        });

        // EnumMemberModel configuration
        modelBuilder.Entity<EnumMemberModel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Source).HasColumnType("TEXT");
            entity.Property(e => e.Value).IsRequired().HasMaxLength(100);
            entity.Property(e => e.File).HasMaxLength(500);
            entity.Property(e => e.LineNumber);
            entity.HasIndex(e => e.EnumTypeId);

            // Configure relationship with TypeModel
            entity
                .HasOne<TypeModel>()
                .WithMany()
                .HasForeignKey(em => em.EnumTypeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // StructMemberModel configuration
        modelBuilder.Entity<StructMemberModel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Source).HasColumnType("TEXT");
            entity.Property(e => e.TypeString).IsRequired().HasMaxLength(500);
            entity.Property(e => e.OverloadIndex).HasDefaultValue(0);
            entity.Property(e => e.DeclarationOrder).HasDefaultValue(0);
            entity.Property(e => e.File).HasMaxLength(500);
            entity.Property(e => e.LineNumber);
            entity.HasIndex(e => e.StructTypeId);
            entity.HasIndex(e => e.TypeReferenceId);
            entity.HasIndex(e => e.FunctionSignatureId);

            // Configure relationship with TypeModel for StructTypeId
            entity
                .HasOne<TypeModel>()
                .WithMany(t => t.StructMembers)
                .HasForeignKey(sm => sm.StructTypeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure relationship with TypeReference
            entity
                .HasOne(sm => sm.TypeReference)
                .WithMany()
                .HasForeignKey(sm => sm.TypeReferenceId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure relationship with FunctionSignatureModel for function pointer members
            entity
                .HasOne(sm => sm.FunctionSignature)
                .WithMany()
                .HasForeignKey(sm => sm.FunctionSignatureId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // FunctionParamModel configuration
        modelBuilder.Entity<FunctionParamModel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255).HasColumnName("ParameterName");
            entity.Property(e => e.Source).HasColumnType("TEXT");
            entity.Property(e => e.ParameterType).IsRequired().HasMaxLength(500);
            entity.Property(e => e.IsFunctionPointerType).HasDefaultValue(false);
            entity.Property(e => e.File).HasMaxLength(500);
            entity.Property(e => e.LineNumber);
            entity.HasIndex(e => e.ParentFunctionSignatureId);
            entity.HasIndex(e => e.TypeReferenceId);
            entity.HasIndex(e => e.NestedFunctionSignatureId);

            // Configure relationship with FunctionSignatureModel (for signature params)
            entity
                .HasOne<FunctionSignatureModel>()
                .WithMany(fs => fs.Parameters)
                .HasForeignKey(fp => fp.ParentFunctionSignatureId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure relationship with TypeReference
            entity
                .HasOne(fp => fp.TypeReference)
                .WithMany()
                .HasForeignKey(fp => fp.TypeReferenceId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure relationship with nested FunctionSignatureModel (for function pointer parameters)
            entity
                .HasOne(fp => fp.NestedFunctionSignature)
                .WithMany()
                .HasForeignKey(fp => fp.NestedFunctionSignatureId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // FunctionSignatureModel configuration
        modelBuilder.Entity<FunctionSignatureModel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ReturnType).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FullyQualifiedName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CallingConvention).HasMaxLength(50);
            entity.Property(e => e.Source).HasColumnType("TEXT");
            entity.Property(e => e.File).HasMaxLength(500);
            entity.Property(e => e.LineNumber);
            entity.HasIndex(e => e.ReturnTypeReferenceId);
            entity.HasIndex(e => e.ReturnFunctionSignatureId);

            // Configure relationship with TypeReference for return type
            entity
                .HasOne(fs => fs.ReturnTypeReference)
                .WithMany()
                .HasForeignKey(fs => fs.ReturnTypeReferenceId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure relationship with nested FunctionSignatureModel for function pointer return types
            entity
                .HasOne(fs => fs.ReturnFunctionSignature)
                .WithMany()
                .HasForeignKey(fs => fs.ReturnFunctionSignatureId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
        });


        // TypeTemplateArgument configuration
        modelBuilder.Entity<TypeTemplateArgument>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ParentTypeId);
            entity.HasIndex(e => e.TypeReferenceId);
            entity.HasIndex(e => e.FunctionSignatureId);

            // Configure the relationship with TypeModel
            entity
                .HasOne<TypeModel>()
                .WithMany(t => t.TemplateArguments)
                .HasForeignKey(ta => ta.ParentTypeId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            // Configure the relationship with TypeReference
            entity
                .HasOne(ta => ta.TypeReference)
                .WithMany()
                .HasForeignKey(ta => ta.TypeReferenceId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure the relationship with FunctionSignatureModel for function pointer template arguments
            entity
                .HasOne(ta => ta.FunctionSignature)
                .WithMany()
                .HasForeignKey(ta => ta.FunctionSignatureId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // TypeInheritance configuration
        modelBuilder.Entity<TypeInheritance>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ParentTypeId);
            entity.HasIndex(e => e.RelatedTypeId);

            // DerivedTypeId is a computed property that delegates to ParentTypeId
            entity.Ignore(e => e.DerivedTypeId);

            // Configure the relationship with TypeModel for ParentTypeId (the derived type)
            entity
                .HasOne<TypeModel>()
                .WithMany(t => t.BaseTypes)
                .HasForeignKey(ti => ti.ParentTypeId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            // Configure the relationship with TypeModel for RelatedTypeId (the base type)
            entity
                .HasOne(ti => ti.RelatedType)
                .WithMany()
                .HasForeignKey(ti => ti.RelatedTypeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // TypeDefModel configuration
        modelBuilder.Entity<TypeDefModel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Namespace).HasMaxLength(500);
            entity.Property(e => e.Source).HasColumnType("TEXT");

            // Create indexes for efficient typedef lookup
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => new { e.Namespace, e.Name });
            entity.HasIndex(e => e.TypeReferenceId);
            entity.HasIndex(e => e.FunctionSignatureId);

            // Ignore computed property (it's not stored in DB)
            entity.Ignore(e => e.FullyQualifiedName);

            // Configure relationship with TypeReference (what it points to)
            entity
                .HasOne(td => td.TypeReference)
                .WithMany()
                .HasForeignKey(td => td.TypeReferenceId)
                .IsRequired()
                .OnDelete(DeleteBehavior.NoAction);

            entity.Property(e => e.File).HasMaxLength(500);
            entity.Property(e => e.LineNumber);

            // Configure relationship with FunctionSignatureModel (for function pointer typedefs)
            entity
                .HasOne(td => td.FunctionSignature)
                .WithMany()
                .HasForeignKey(td => td.FunctionSignatureId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // FunctionBodyModel configuration
        modelBuilder.Entity<FunctionBodyModel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FullyQualifiedName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.BodyText).HasColumnType("TEXT");
            entity.Property(e => e.Source).HasColumnType("TEXT");
            entity.Property(e => e.File).HasMaxLength(500);
            entity.Property(e => e.LineNumber);

            entity.HasIndex(e => e.FullyQualifiedName);
            entity.HasIndex(e => e.FunctionSignatureId);
            entity.HasIndex(e => e.ParentId);

            entity
                .HasOne(fb => fb.FunctionSignature)
                .WithMany()
                .HasForeignKey(fb => fb.FunctionSignatureId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            entity
                .HasOne(fb => fb.ParentType)
                .WithMany()
                .HasForeignKey(fb => fb.ParentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // StaticVariableModel configuration
        modelBuilder.Entity<StaticVariableModel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Address).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
            entity.Property(e => e.TypeString).IsRequired().HasMaxLength(500);
            entity.Property(e => e.GlobalType).IsRequired().HasColumnType("TEXT");
            entity.Property(e => e.Value).HasColumnType("TEXT");
            entity.HasIndex(e => e.ParentTypeId);
            entity.HasIndex(e => e.TypeReferenceId);

            entity
                .HasOne(sv => sv.ParentType)
                .WithMany(t => t.StaticVariables)
                .HasForeignKey(sv => sv.ParentTypeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(sv => sv.TypeReference)
                .WithMany()
                .HasForeignKey(sv => sv.TypeReferenceId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
        });

        base.OnModelCreating(modelBuilder);
    }
}
