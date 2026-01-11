using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Services;
using Moq;
using Xunit;
using Xunit.Abstractions;
using System.Collections.Generic;

namespace ACDecompileParser.Shared.Tests.Lib.Output.CSharp;

public class ArrayStructSizeTests
{
    private readonly Mock<ITypeRepository> _mockRepository;
    private readonly CSharpBindingsGenerator _generator;
    private readonly OffsetCalculationService _offsetService;
    private readonly ITestOutputHelper _testOutput;

    public ArrayStructSizeTests(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _mockRepository = new Mock<ITypeRepository>();

        // We need a real OffsetCalculationService to calculate sizes
        // We will mock the repository calls it makes
        _offsetService = new OffsetCalculationService(_mockRepository.Object);

        // Pass the offset service to the generator (once we update the constructor)
        // For now, we'll instantiate it normally, and update the test as we change the code
        _generator = new CSharpBindingsGenerator(_mockRepository.Object, _offsetService);
    }

    [Fact]
    public void Test_ArrayOfStructs_UsesHardcodedSize()
    {
        // Define a struct "MyStruct" with known size
        // Let's say it has 2 ints, so size is 8 bytes.
        var myStruct = new TypeModel
        {
            Id = 100,
            BaseName = "MyStruct",
            Type = TypeType.Struct,
            StructMembers = new List<StructMemberModel>
            {
                new() { Name = "a", TypeString = "int", DeclarationOrder = 1 },
                new() { Name = "b", TypeString = "int", DeclarationOrder = 2 }
            }
        };

        // Cache setup for OffsetCalculationService
        _mockRepository.Setup(r => r.GetTypesByTypeType(TypeType.Struct))
            .Returns(new List<TypeModel> { myStruct });

        _mockRepository.Setup(r => r.GetStructMembersWithRelatedTypes(myStruct.Id))
            .Returns(myStruct.StructMembers);

        _mockRepository.Setup(r => r.GetBaseTypesWithRelatedTypes(It.IsAny<int>()))
            .Returns(new List<TypeInheritance>());

        _mockRepository.Setup(r => r.GetTypeById(myStruct.Id))
            .Returns(myStruct);

        // Define the container struct with an array of MyStruct
        // Member: MyStruct m_items[10]
        var containerType = new TypeModel
        {
            Id = 1,
            BaseName = "Container",
            Type = TypeType.Struct,
            StructMembers = new List<StructMemberModel>
            {
                new()
                {
                    Name = "m_items",
                    TypeString = "MyStruct",
                    DeclarationOrder = 1,
                    TypeReferenceId = 50,
                    TypeReference = new TypeReference
                    {
                        Id = 50,
                        IsArray = true,
                        ArraySize = 10,
                        TypeString = "MyStruct",
                        ReferencedTypeId = 100, // Points to MyStruct
                        ReferencedType = myStruct // Navigation property
                    }
                }
            }
        };

        // Ensure the mock returns the referenced type when looked up
        _mockRepository.Setup(r => r.GetTypeReferenceById(50))
            .Returns(containerType.StructMembers[0].TypeReference);

        // Pre-calculate offsets to populate the cache inside OffsetCalculationService
        // This is important because the generator might use the service to get the size
        _offsetService.CalculateTypeSize(myStruct);

        // Generate code
        var output = _generator.Generate(containerType);
        _testOutput.WriteLine(output);

        // Expected backing field should use literal size (8), not sizeof(MyStruct)
        // 10 * 8 = 80
        Assert.Contains("public fixed byte m_items_Raw[80];", output);

        // Expected helper property should cast to correct type pointer
        Assert.Contains(
            $"public {CSharpBindingsGenerator.NAMESPACE}.MyStruct* m_items => ({CSharpBindingsGenerator.NAMESPACE}.MyStruct*)System.Runtime.CompilerServices.Unsafe.AsPointer(ref m_items_Raw[0]);",
            output);
    }
}
