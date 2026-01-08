using System.Collections.Generic;
using System.Linq;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Services;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Tests.Lib.Storage.Mocks;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Services;

public class OffsetCalculationAnalysisTests
{
    [Fact]
    public void CalculateTypeSize_StructWithPadding_ReturnsAlignedSize()
    {
        // StructA { int a; char b; }
        // Size should be 8 (4 + 1 + 3 padding) because alignment is 4.
        
        var repo = new TestTypeRepository();
        var structType = new TypeModel { Id = 1, BaseName = "StructA", Type = TypeType.Struct };
        repo.InsertType(structType);

        repo.InsertStructMember(new StructMemberModel { Id = 1, Name = "a", TypeString = "int", StructTypeId = 1, DeclarationOrder = 1 });
        repo.InsertStructMember(new StructMemberModel { Id = 2, Name = "b", TypeString = "char", StructTypeId = 1, DeclarationOrder = 2 });

        repo.InsertType(new TypeModel { BaseName = "int", Type = TypeType.Primitive });
        repo.InsertType(new TypeModel { BaseName = "char", Type = TypeType.Primitive });

        var service = new OffsetCalculationService(repo);
        
        // Act
        service.CalculateAndApplyOffsets();
        int size = service.CalculateTypeSize(structType);

        // Assert
        Assert.Equal(8, size); 
    }

    [Fact]
    public void CalculateStructMemberOffsets_MultipleInheritance_AlignsSecondBase()
    {
        // Base1 { int a; char b; } -> Size 8 (implied, if padded) or 5 (if unpadded)
        // Base2 { int c; } -> Size 4
        // Derived : Base1, Base2 { char d; }
        
        // Expected layout:
        // 0: Base1 (size 8 with padding)
        // 8: Base2 (starts at 8 because aligned to 4)
        // 12: d
        
        // If Base1 is NOT padded (size 5):
        // 0: Base1
        // 5: Padding
        // 8: Base2
        // 12: d
        
        // Current suspected logic:
        // Base1 size = 5 (if unpadded)
        // Base2 size = 4
        // BaseClassSize = 5 + 4 = 9
        // d starts at 9
        
        var repo = new TestTypeRepository();
        
        var base1 = new TypeModel { Id = 1, BaseName = "Base1", Type = TypeType.Struct };
        var base2 = new TypeModel { Id = 2, BaseName = "Base2", Type = TypeType.Struct };
        var derived = new TypeModel { Id = 3, BaseName = "Derived", Type = TypeType.Struct };
        
        repo.InsertType(base1);
        repo.InsertType(base2);
        repo.InsertType(derived);
        
        // Base1 members
        repo.InsertStructMember(new StructMemberModel { Id = 10, Name = "a", TypeString = "int", StructTypeId = 1, DeclarationOrder = 1 });
        repo.InsertStructMember(new StructMemberModel { Id = 11, Name = "b", TypeString = "char", StructTypeId = 1, DeclarationOrder = 2 });
        
        // Base2 members
        repo.InsertStructMember(new StructMemberModel { Id = 20, Name = "c", TypeString = "int", StructTypeId = 2, DeclarationOrder = 1 });
        
        // Derived members
        repo.InsertStructMember(new StructMemberModel { Id = 30, Name = "d", TypeString = "char", StructTypeId = 3, DeclarationOrder = 1 });

        // Inheritance
        repo.InsertTypeInheritance(new TypeInheritance { ParentTypeId = 3, RelatedTypeId = 1, RelatedType = base1 }); // Derived -> Base1
        repo.InsertTypeInheritance(new TypeInheritance { ParentTypeId = 3, RelatedTypeId = 2, RelatedType = base2 }); // Derived -> Base2

        // Primitives
        repo.InsertType(new TypeModel { BaseName = "int", Type = TypeType.Primitive });
        repo.InsertType(new TypeModel { BaseName = "char", Type = TypeType.Primitive });

        var service = new OffsetCalculationService(repo);
        
        // Act
        service.CalculateAndApplyOffsets();
        
        var members = repo.GetStructMembers(3); // Get derived members
        var d = members.First(m => m.Name == "d");
        
        // Assert
        // If Base1 size is 5 (unpadded) -> Base2 should align to 8. Base2 end = 12. d at 12.
        // If Base1 size is 8 (padded) -> Base2 starts at 8. Base2 end = 12. d at 12.
        Assert.Equal(12, d.Offset);
    }
}
