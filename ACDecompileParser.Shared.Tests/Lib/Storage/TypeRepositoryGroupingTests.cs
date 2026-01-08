using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Xunit;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;

namespace ACDecompileParser.Shared.Tests.Lib.Storage;

public class TypeRepositoryGroupingTests
{
    private DbContextOptions<TypeContext> CreateNewContextOptions()
    {
        return new DbContextOptionsBuilder<TypeContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    private TypeContext CreateContext()
    {
        return new TypeContext(CreateNewContextOptions());
    }

    [Fact]
    public void GetTypesForGroup_ExcludesUnrelatedTypes_WithSimilarNames()
    {
        // Arrange
        using var context = CreateContext();
        using var repository = new TypeRepository(context);

        // 1. The main type we are grouping
        var uiRegion = new TypeModel
        {
            BaseName = "UIRegion",
            Namespace = "",
            Type = TypeType.Struct,
            StoredFullyQualifiedName = "UIRegion",
            // BaseTypePath points to itself for root types (or is set by logic)
            BaseTypePath = "UIRegion"
        };

        // 2. A nested type that *should* be included
        var uiRegionState = new TypeModel
        {
            BaseName = "State",
            Namespace = "UIRegion", // UIRegion::State
            Type = TypeType.Enum,
            StoredFullyQualifiedName = "UIRegion::State",
            BaseTypePath = "UIRegion" // Grouped with UIRegion
        };

        // 3. A type that *uses* UIRegion in template args but is NOT part of the group
        // This is the regression case: HashList<UIRegion *, ...>
        var hashList = new TypeModel
        {
            BaseName = "HashList<UIRegion *,UIRegion *,1>",
            Namespace = "",
            Type = TypeType.Struct,
            StoredFullyQualifiedName = "HashList<UIRegion *,UIRegion *,1>",
            BaseTypePath = "HashList<UIRegion *,UIRegion *,1>" // Points to itself
        };

        // 4. A nested type of the unrelated type
        var hashListData = new TypeModel
        {
            BaseName = "HashListData",
            Namespace = "HashList<UIRegion *,UIRegion *,1>",
            Type = TypeType.Struct,
            StoredFullyQualifiedName = "HashList<UIRegion *,UIRegion *,1>::HashListData",
            BaseTypePath = "HashList<UIRegion *,UIRegion *,1>" // Grouped with HashList
        };

        repository.InsertType(uiRegion);
        repository.InsertType(uiRegionState);
        repository.InsertType(hashList);
        repository.InsertType(hashListData);
        repository.SaveChanges();

        // Act
        // We want the group for "UIRegion"
        var group = repository.GetTypesForGroup("UIRegion", "");

        // Assert
        // Should contain UIRegion and UIRegion::State
        Assert.Contains(group, t => t.BaseName == "UIRegion");
        Assert.Contains(group, t => t.BaseName == "State"); // Nested type

        // Should NOT contain HashList or its members
        Assert.DoesNotContain(group, t => t.BaseName.Contains("HashList"));

        Assert.Equal(2, group.Count);
    }
}
