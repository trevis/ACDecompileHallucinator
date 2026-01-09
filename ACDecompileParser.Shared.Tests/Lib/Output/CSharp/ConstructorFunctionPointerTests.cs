using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using ACDecompileParser.Shared.Lib.Storage;
using Moq;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output.CSharp;

public class ConstructorFunctionPointerTests
{
    private readonly Mock<ITypeRepository> _mockRepository;
    private readonly CSharpBindingsGenerator _generator;
    private readonly Xunit.Abstractions.ITestOutputHelper _testOutput;

    public ConstructorFunctionPointerTests(Xunit.Abstractions.ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _mockRepository = new Mock<ITypeRepository>();
        _generator = new CSharpBindingsGenerator(_mockRepository.Object);
    }

    [Fact]
    public void Test_Constructor_With_FunctionPointer_Parameter()
    {
        // Simulate DBOCache constructor: public DBOCache(delegate* unmanaged[Cdecl]<ACBindings.DBObj*> allocator, uint dbtype)
        var dboCacheType = new TypeModel
        {
            Id = 1,
            BaseName = "DBOCache",
            Type = TypeType.Struct,
            FunctionBodies = new List<FunctionBodyModel>
            {
                new()
                {
                    Id = 101,
                    FullyQualifiedName = "DBOCache::DBOCache",
                    FunctionSignature = new FunctionSignatureModel
                    {
                        ReturnType = "void",
                        CallingConvention = "Thiscall",
                        Parameters = new List<FunctionParamModel>
                        {
                            new() { Name = "this", ParameterType = "DBOCache*", Position = 0 },
                            new()
                            {
                                Name = "allocator",
                                ParameterType = "DBObj*(__cdecl*)()",
                                Position = 1,
                                IsFunctionPointerType = true,
                                NestedFunctionSignature = new FunctionSignatureModel
                                {
                                    ReturnType = "DBObj*",
                                    CallingConvention = "Cdecl",
                                    Parameters = new List<FunctionParamModel>()
                                }
                            },
                            new() { Name = "dbtype", ParameterType = "unsigned int", Position = 2 }
                        }
                    },
                    Offset = 0x00417510
                }
            }
        };

        var output = _generator.Generate(dboCacheType);
        _testOutput.WriteLine(output);

        // Verify the constructor signature
        // BEFORE FIX: public DBOCache(System.IntPtr allocator, uint dbtype)
        // AFTER FIX: public DBOCache(delegate* unmanaged[Cdecl]<ACBindings.DBObj*> allocator, uint dbtype)
        
        Assert.Contains("public DBOCache(delegate* unmanaged[Cdecl]<ACBindings.DBObj*> allocator, uint dbtype)", output);
        
        // Also verify the internal call casts it correctly (this part might already be correct or need adjustment, but the signature is key)
        Assert.Contains("_ConstructorInternal(allocator, dbtype)", output); 
    }
}
