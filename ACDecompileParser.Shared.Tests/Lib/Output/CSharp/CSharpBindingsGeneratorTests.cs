using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using ACDecompileParser.Shared.Lib.Storage;
using Moq;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output.CSharp;

public class CSharpBindingsGeneratorTests
{
    private readonly Mock<ITypeRepository> _mockRepository;
    private readonly CSharpBindingsGenerator _generator;
    private readonly Xunit.Abstractions.ITestOutputHelper _testOutput;

    public CSharpBindingsGeneratorTests(Xunit.Abstractions.ITestOutputHelper testOutput)
    {
        _testOutput = testOutput;
        _mockRepository = new Mock<ITypeRepository>();
        _generator = new CSharpBindingsGenerator(_mockRepository.Object);
    }

    [Fact]
    public void Test_Render_Basic_Bindings()
    {
        var renderType = new TypeModel
        {
            Id = 1,
            BaseName = "Render",
            Type = TypeType.Struct,
            StructMembers = new List<StructMemberModel>
            {
                new() { Name = "__vftable", TypeString = "void*", DeclarationOrder = 1 },
                new() { Name = "m_nDisplayAdapter", TypeString = "unsigned int", DeclarationOrder = 2 }
            },
            FunctionBodies = new List<FunctionBodyModel>
            {
                new()
                {
                    Id = 101,
                    FullyQualifiedName = "Render::Render",
                    FunctionSignature = new FunctionSignatureModel
                    {
                        ReturnType = "void",
                        CallingConvention = "Thiscall",
                        Parameters = new List<FunctionParamModel>
                        {
                            new() { Name = "this", ParameterType = "Render*", Position = 0 }
                        }
                    },
                    Offset = 0x0059EDE0
                },
                new()
                {
                    Id = 102,
                    FullyQualifiedName = "Render::~Render",
                    FunctionSignature = new FunctionSignatureModel
                    {
                        ReturnType = "void",
                        CallingConvention = "Thiscall",
                        Parameters = new List<FunctionParamModel>
                        {
                            new() { Name = "this", ParameterType = "Render*", Position = 0 }
                        }
                    },
                    Offset = 0x0059EDE1
                },
                new()
                {
                    Id = 103,
                    FullyQualifiedName = "Render::Set3DViewInternal",
                    FunctionSignature = new FunctionSignatureModel
                    {
                        ReturnType = "int",
                        CallingConvention = "Thiscall",
                        Parameters = new List<FunctionParamModel>
                        {
                            new() { Name = "this", ParameterType = "Render*", Position = 0 },
                            new() { Name = "x", ParameterType = "int", Position = 1 },
                            new() { Name = "y", ParameterType = "int", Position = 2 }
                        }
                    },
                    Offset = 0x0054FC80
                }
            }
        };

        var output = _generator.Generate(renderType);
        _testOutput.WriteLine(output);

        // Basic verification
        Assert.Contains("public unsafe struct Render", output);
        Assert.Contains(": System.IDisposable", output);
        Assert.Contains("public void* __vftable;", output);
        Assert.Contains("public uint m_nDisplayAdapter;", output);

        // Constructor/Destructor internal
        Assert.Contains("_ConstructorInternal", output);
        Assert.Contains("_DestructorInternal", output);

        // Constructors/Dispose wrapping
        Assert.Contains("public Render()", output);
        Assert.Contains("_ConstructorInternal(", output);
        Assert.Contains("public void Dispose()", output);
        Assert.Contains("_DestructorInternal(", output);

        // Method mapping
        Assert.Contains("Set3DViewInternal(int x, int y)", output);
    }

    [Fact]
    public void Test_ACRender_Inheritance_Bindings()
    {
        var renderType = new TypeModel
        {
            Id = 1,
            BaseName = "Render",
            Type = TypeType.Struct,
            // Function Bodies needed for pulling
            FunctionBodies = new List<FunctionBodyModel>
            {
                new()
                {
                    Id = 101, FullyQualifiedName = "Render::Set3DView",
                    FunctionSignature = new FunctionSignatureModel
                    {
                        ReturnType = "int", CallingConvention = "Cdecl",
                        Parameters = new List<FunctionParamModel>
                        {
                            new() { Name = "x", ParameterType = "int", Position = 0 },
                            new() { Name = "y", ParameterType = "int", Position = 1 }
                        }
                    }
                },
                new()
                {
                    Id = 102, FullyQualifiedName = "Render::Set3DViewInternal",
                    FunctionSignature = new FunctionSignatureModel
                    {
                        ReturnType = "int", CallingConvention = "Thiscall",
                        Parameters = new List<FunctionParamModel>
                        {
                            new() { Name = "this", ParameterType = "Render*" },
                            new() { Name = "x", ParameterType = "int" }, new() { Name = "y", ParameterType = "int" }
                        }
                    }
                }
            }
        };

        // We need the repo to return function bodies for Render so recursive pulling works
        var renderBodies = new List<FunctionBodyModel>
        {
            new()
            {
                Id = 103,
                FullyQualifiedName = "Render::Set3DViewInternal",
                FunctionSignature = new FunctionSignatureModel
                {
                    ReturnType = "int",
                    CallingConvention = "Thiscall",
                    Parameters = new List<FunctionParamModel>
                    {
                        new() { Name = "this", ParameterType = "Render*", Position = 0 },
                        new() { Name = "x", ParameterType = "int", Position = 1 },
                        new() { Name = "y", ParameterType = "int", Position = 2 }
                    }
                },
                Offset = 0x0054FC80
            },
            new()
            {
                Id = 104,
                FullyQualifiedName = "Render::Set3DView",
                FunctionSignature = new FunctionSignatureModel
                {
                    ReturnType = "int",
                    CallingConvention = "Cdecl",
                    Parameters = new List<FunctionParamModel>
                    {
                        new() { Name = "x", ParameterType = "int", Position = 0 },
                        new() { Name = "y", ParameterType = "int", Position = 1 }
                    }
                },
                Offset = 0x0054BE30
            },
            new()
            {
                Id = 102,
                FullyQualifiedName = "Render::~Render",
                FunctionSignature = new FunctionSignatureModel
                {
                    ReturnType = "void",
                    CallingConvention = "Thiscall",
                    Parameters = new List<FunctionParamModel>
                    {
                        new() { Name = "this", ParameterType = "Render*", Position = 0 }
                    }
                },
                Offset = 0x0059EDE1
            }
        };

        _mockRepository.Setup(r => r.GetFunctionBodiesForType(1)).Returns(renderBodies);
        _mockRepository.Setup(r => r.GetTypesForGroup("Render", It.IsAny<string>()))
            .Returns(new List<TypeModel> { renderType });

        var acRenderType = new TypeModel
        {
            Id = 2,
            BaseName = "ACRender",
            Type = TypeType.Struct,
            BaseTypes = new List<TypeInheritance>
            {
                new() { RelatedType = renderType, RelatedTypeId = 1 }
            },
            FunctionBodies = new List<FunctionBodyModel>
            {
                new()
                {
                    Id = 201,
                    FullyQualifiedName = "ACRender::ACRender",
                    FunctionSignature = new FunctionSignatureModel
                    {
                        ReturnType = "void",
                        CallingConvention = "Thiscall",
                        Parameters = new List<FunctionParamModel>
                        {
                            new() { Name = "this", ParameterType = "ACRender*", Position = 0 },
                            new() { Name = "index", ParameterType = "int", Position = 1 }
                        }
                    },
                    Offset = 0x0059EED4
                }
            }
        };

        var output = _generator.Generate(acRenderType);
        _testOutput.WriteLine(output);

        Assert.Contains("public unsafe struct ACRender", output);
        Assert.Contains(": System.IDisposable", output);
        Assert.Contains("public Render BaseClass_Render;", output);

        // Constructor mapping
        Assert.Contains("public ACRender(int index)", output);
        Assert.Contains("_ConstructorInternal(index);", output);

        // Verify Dispose calls base
        Assert.Contains("public void Dispose()", output);
        Assert.Contains("BaseClass_Render.Dispose();", output);

        // Verify pulled methods
        // Set3DView is Cdecl (Static), so wrapper should be static and call Type.Method
        Assert.Contains("public static int Set3DView(int x, int y) => Render.Set3DView(x, y);", output);

        // Set3DViewInternal is Thiscall (Instance), so wrapper is instance and calls BaseField.Method
        // Note: arguments for wrapper call do NOT include 'ref this' explicitly in C# call syntax
        Assert.Contains("public int Set3DViewInternal(int x, int y) => BaseClass_Render.Set3DViewInternal(x, y);",
            output);
    }
    [Fact]
    public void Test_Using_Namespace_Replacement()
    {
        var baseType = new TypeModel
        {
            Id = 1,
            BaseName = "Core::Base",
            Type = TypeType.Struct
        };

        var derivedType = new TypeModel
        {
            Id = 2,
            BaseName = "App::Derived",
            Type = TypeType.Struct,
            BaseTypes = new List<TypeInheritance>
            {
                new() { RelatedType = baseType, RelatedTypeId = 1 }
            },
            StructMembers = new List<StructMemberModel>
            {
                new() { Name = "m_ptr", TypeString = "Core::Base*", DeclarationOrder = 1 },
                new() { Name = "m_val", TypeString = "Core::Value", DeclarationOrder = 2 }
            },
            FunctionBodies = new List<FunctionBodyModel>
            {
                new()
                {
                    Id = 201,
                    FullyQualifiedName = "App::Derived::GetBase",
                    FunctionSignature = new FunctionSignatureModel
                    {
                        ReturnType = "Core::Base*",
                        CallingConvention = "Thiscall",
                        Parameters = new List<FunctionParamModel>
                        {
                            new() { Name = "this", ParameterType = "App::Derived*", Position = 0 }
                        }
                    },
                    Offset = 0x12345678
                }
            }
        };

        // We need static method too to test that path
        baseType.FunctionBodies = new List<FunctionBodyModel>
        {
            new()
            {
                Id = 101,
                FullyQualifiedName = "Core::Base::StaticMethod",
                FunctionSignature = new FunctionSignatureModel
                {
                    ReturnType = "int",
                    CallingConvention = "Cdecl",
                    Parameters = new List<FunctionParamModel>()
                },
                Offset = 0x87654321
            }
        };

        _mockRepository.Setup(r => r.GetFunctionBodiesForType(1)).Returns(baseType.FunctionBodies);

        var output = _generator.Generate(derivedType);
        _testOutput.WriteLine(output);

        // Verify namespace replacement in member types
        Assert.Contains("public void* m_ptr;", output); // Pointers are always void* but MapType handles it
        Assert.Contains("public Core.Value m_val;", output);

        // Verify namespace replacement in base class
        Assert.Contains("public Core.Base BaseClass_Core_Base;", output);

        // Verify namespace replacement in return types
        Assert.Contains("public void* GetBase()", output);

        // Verify pulled up static method
        Assert.Contains("public static int StaticMethod() => Core.Base.StaticMethod();", output);
    }
}
