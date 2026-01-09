using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using ACDecompileParser.Shared.Lib.Storage;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ACDecompileParser.Shared.Tests.Lib.Output.CSharp;

public class ArrayFieldGenerationTests
{
    private readonly Mock<ITypeRepository> _mockRepository;
    private readonly CSharpBindingsGenerator _generator;
    private readonly ITestOutputHelper _testOutput;

    public ArrayFieldGenerationTests(ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _mockRepository = new Mock<ITypeRepository>();
        _generator = new CSharpBindingsGenerator(_mockRepository.Object);
    }

    [Fact]
    public void Test_ArrayOfPointers_Generation()
    {
        // Member: CAsyncStateData* m_aInplaceBuckets[23]
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "CAsyncStateMachine",
            Type = TypeType.Struct,
            StructMembers = new List<StructMemberModel>
            {
                new()
                {
                    Name = "m_aInplaceBuckets",
                    TypeString = "CAsyncStateMachine::CAsyncStateData*",
                    DeclarationOrder = 1,
                    TypeReference = new TypeReference
                    {
                        IsArray = true,
                        ArraySize = 23,
                        IsPointer = true,
                        ReferencedTypeId = 100, // Simulate resolved type
                        TypeString = "CAsyncStateMachine::CAsyncStateData*"
                    }
                }
            }
        };

        var output = _generator.Generate(type);
        _testOutput.WriteLine(output);

        // Expected backing field
        Assert.Contains(
            "public fixed byte m_aInplaceBuckets_Raw[23 * 4];",
            output);
        // Expected helper property
        Assert.Contains(
            "public ACBindings.CAsyncStateMachine.CAsyncStateData** m_aInplaceBuckets => (ACBindings.CAsyncStateMachine.CAsyncStateData**)System.Runtime.CompilerServices.Unsafe.AsPointer(ref m_aInplaceBuckets_Raw[0]);",
            output);
    }


    [Fact]
    public void Test_ArrayOfStructs_Generation()
    {
        // Member: MyStruct m_items[10]
        var type = new TypeModel
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
                    TypeReference = new TypeReference
                    {
                        IsArray = true,
                        ArraySize = 10,
                        TypeString = "MyStruct"
                    }
                }
            }
        };

        var output = _generator.Generate(type);
        _testOutput.WriteLine(output);

        // Expected backing field
        Assert.Contains("public fixed byte m_items_Raw[10 * sizeof(ACBindings.MyStruct)];", output);
        // Expected helper property
        Assert.Contains(
            "public ACBindings.MyStruct* m_items => (ACBindings.MyStruct*)System.Runtime.CompilerServices.Unsafe.AsPointer(ref m_items_Raw[0]);",
            output);
    }

    [Fact]
    public void Test_PrimitiveArray_RemainsFixed()
    {
        // Member: int m_values[5]
        var type = new TypeModel
        {
            Id = 1,
            BaseName = "Tracker",
            Type = TypeType.Struct,
            StructMembers = new List<StructMemberModel>
            {
                new()
                {
                    Name = "m_values",
                    TypeString = "int",
                    DeclarationOrder = 1,
                    TypeReference = new TypeReference
                    {
                        IsArray = true,
                        ArraySize = 5
                    }
                }
            }
        };

        var output = _generator.Generate(type);
        _testOutput.WriteLine(output);

        // Should use standard fixed buffer for primitives
        Assert.Contains("public fixed int m_values[5];", output);
        Assert.DoesNotContain("m_values_Raw", output);
    }
}
