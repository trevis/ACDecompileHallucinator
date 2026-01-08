using ACDecompileParser.Shared.Lib.Models;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Models;

public class StructModelTests
{
    [Fact]
    public void CanCreateBasicStruct()
    {
        // Arrange
        var structModel = new StructTypeModel
        {
            Name = "UIElement_Scrollable",
            Namespace = string.Empty
        };

        var members = new List<StructMemberModel>
        {
            new StructMemberModel { Name = "m_iScrollableWidth", TypeString = "int", Offset = 0 },
            new StructMemberModel { Name = "m_iScrollableHeight", TypeString = "int", Offset = 0 }
        };

        // Act & Assert
        Assert.Equal("UIElement_Scrollable", structModel.Name);
        Assert.Equal("UIElement_Scrollable", structModel.FullyQualifiedName);
        Assert.Equal(string.Empty, structModel.Namespace);
        Assert.Equal(TypeType.Struct, structModel.Type);

        Assert.Equal(2, members.Count);
        Assert.Equal("m_iScrollableWidth", members[0].Name);
        Assert.Equal("int", members[0].TypeString);
        Assert.Equal("m_iScrollableHeight", members[1].Name);
        Assert.Equal("int", members[1].TypeString);
    }

    [Fact]
    public void CanCreateTemplatedStruct()
    {
        // Arrange
        var structModel = new StructTypeModel
        {
            Name = "SmartArray",
            Namespace = string.Empty
        };

        var templateArgs = new List<TypeReference>
        {
            new TypeReference { TypeString = "ContextMenuData" },
            new TypeReference { TypeString = "1" }
        };

        var members = new List<StructMemberModel>
        {
            new StructMemberModel
            {
                Name = "m_data", TypeString = "ContextMenuData", TypeReference = new TypeReference { IsPointer = true },
                Offset = 0
            },
            new StructMemberModel { Name = "m_sizeAndDeallocate", TypeString = "unsigned int", Offset = 0 },
            new StructMemberModel { Name = "m_num", TypeString = "unsigned int", Offset = 0 }
        };

        // Act & Assert
        Assert.Equal("SmartArray", structModel.Name);
        Assert.Equal("SmartArray", structModel.FullyQualifiedName);
        Assert.Equal(string.Empty, structModel.Namespace);
        Assert.Equal(TypeType.Struct, structModel.Type);

        Assert.Equal(2, templateArgs.Count);
        Assert.Equal("ContextMenuData", templateArgs[0].TypeString);
        Assert.Equal("1", templateArgs[1].TypeString);

        Assert.Equal(3, members.Count);
        Assert.Equal("m_data", members[0].Name);
        Assert.Equal("ContextMenuData", members[0].TypeString);
        Assert.True(members[0].TypeReference?.IsPointer ?? false);
        Assert.Equal("m_sizeAndDeallocate", members[1].Name);
        Assert.Equal("unsigned int", members[1].TypeString);
        Assert.Equal("m_num", members[2].Name);
        Assert.Equal("unsigned int", members[2].TypeString);
    }

    [Fact]
    public void CanCreateStructWithInheritance()
    {
        // Arrange
        var structModel = new StructTypeModel
        {
            Name = "DerivedStruct",
            Namespace = "TestNamespace"
        };

        var baseTypes = new List<string>
        {
            "BaseClass"
        };

        // Act & Assert
        Assert.Equal("DerivedStruct", structModel.Name);
        Assert.Equal("TestNamespace", structModel.Namespace);
        Assert.Equal("TestNamespace::DerivedStruct", structModel.FullyQualifiedName);
        Assert.Equal(TypeType.Struct, structModel.Type);

        Assert.Single(baseTypes);
        Assert.Equal("BaseClass", baseTypes[0]);
    }

    [Fact]
    public void CanCreateVTableStruct()
    {
        // Arrange
        var structModel = new StructTypeModel
        {
            Name = "ContextMenu_vtbl",
            Namespace = string.Empty
        };

        var members = new List<StructMemberModel>
        {
            new StructMemberModel
            {
                Name = "~InputActionCallback",
                TypeString = "void",
                IsFunctionPointer = true,
                FunctionSignature = new FunctionSignatureModel
                {
                    ReturnTypeReference = new TypeReference { TypeString = "void" },
                    CallingConvention = "__thiscall",
                    Parameters = new List<FunctionParamModel>
                    {
                        new FunctionParamModel
                        {
                            ParameterType = "InputActionCallback",
                            Name = "this",
                            TypeReference = new TypeReference { IsPointer = true }
                        }
                    }
                }
            }
        };

        // Act & Assert
        Assert.Equal("ContextMenu_vtbl", structModel.Name);
        Assert.Equal("ContextMenu_vtbl", structModel.FullyQualifiedName);
        Assert.Equal(string.Empty, structModel.Namespace);
        Assert.Equal(TypeType.Struct, structModel.Type);

        Assert.Single(members);
        Assert.Equal("~InputActionCallback", members[0].Name);
        Assert.Equal("void", members[0].TypeString);
        Assert.True(members[0].IsFunctionPointer);
        Assert.Equal("void", members[0].FunctionSignature!.ReturnTypeReference?.TypeString ?? string.Empty);
        Assert.Equal("__thiscall", members[0].FunctionSignature!.CallingConvention);
        Assert.Single(members[0].FunctionSignature!.Parameters);
        Assert.Equal("InputActionCallback", members[0].FunctionSignature!.Parameters[0].ParameterType);
        Assert.Equal("this", members[0].FunctionSignature!.Parameters[0].Name);
        Assert.True(members[0].FunctionSignature!.Parameters[0].TypeReference?.IsPointer ?? false);
    }

    [Fact]
    public void CanCreateStructWithOffsets()
    {
        // Arrange
        var structModel = new StructTypeModel
        {
            Name = "UIElement_Scrollable",
            Namespace = string.Empty
        };

        var members = new List<StructMemberModel>
        {
            new StructMemberModel { Name = "m_iScrollableWidth", TypeString = "int", Offset = 8 }, // 0x0008 = 8
            new StructMemberModel { Name = "m_iScrollableHeight", TypeString = "int", Offset = 12 }, // 0x000C = 12
            new StructMemberModel { Name = "m_fScrollFactor", TypeString = "float", Offset = 16 } // 0x0010 = 16
        };

        // Act & Assert
        Assert.Equal("UIElement_Scrollable", structModel.Name);
        Assert.Equal("UIElement_Scrollable", structModel.FullyQualifiedName);
        Assert.Equal(string.Empty, structModel.Namespace);
        Assert.Equal(TypeType.Struct, structModel.Type);

        Assert.Equal(3, members.Count);
        Assert.Equal("m_iScrollableWidth", members[0].Name);
        Assert.Equal("int", members[0].TypeString);
        Assert.Equal(8, members[0].Offset);

        Assert.Equal("m_iScrollableHeight", members[1].Name);
        Assert.Equal("int", members[1].TypeString);
        Assert.Equal(12, members[1].Offset);

        Assert.Equal("m_fScrollFactor", members[2].Name);
        Assert.Equal("float", members[2].TypeString);
        Assert.Equal(16, members[2].Offset);
    }

    [Fact]
    public void CanCreateStructWithVirtualFunctions()
    {
        // Arrange
        var structModel = new StructTypeModel
        {
            Name = "_SECURITY_FUNCTION_TABLE_W",
            Namespace = string.Empty
        };

        var members = new List<StructMemberModel>
        {
            new StructMemberModel { Name = "dwVersion", TypeString = "unsigned int", IsFunctionPointer = false },
            new StructMemberModel
            {
                Name = "QuerySecurityPackageInfoW",
                TypeString = "int",
                IsFunctionPointer = true,
                FunctionSignature = new FunctionSignatureModel
                {
                    ReturnTypeReference = new TypeReference { TypeString = "int" },
                    CallingConvention = "__stdcall",
                    Parameters = new List<FunctionParamModel>
                    {
                        new FunctionParamModel
                        {
                            ParameterType = "unsigned __int16",
                            Name = "__param1",
                            TypeReference = new TypeReference { IsPointer = true }
                        },
                        new FunctionParamModel
                        {
                            ParameterType = "_SecPkgInfoW",
                            Name = "__param2",
                            TypeReference = new TypeReference { IsPointer = true }
                        }
                    }
                }
            },
            new StructMemberModel
            {
                Name = "Reserved3", TypeString = "void", TypeReference = new TypeReference { IsPointer = true },
                IsFunctionPointer = false
            },
            new StructMemberModel
            {
                Name = "AddCredentialsW",
                TypeString = "int",
                IsFunctionPointer = true,
                FunctionSignature = new FunctionSignatureModel
                {
                    ReturnTypeReference = new TypeReference { TypeString = "int" },
                    CallingConvention = "__stdcall",
                    Parameters = new List<FunctionParamModel>
                    {
                        new FunctionParamModel
                        {
                            ParameterType = "void", Name = "__param1",
                            TypeReference = new TypeReference { IsPointer = true }
                        },
                        new FunctionParamModel
                        {
                            ParameterType = "unsigned __int16", Name = "__param2",
                            TypeReference = new TypeReference { IsPointer = true }
                        },
                        new FunctionParamModel
                        {
                            ParameterType = "unsigned __int16", Name = "__param3",
                            TypeReference = new TypeReference { IsPointer = true }
                        },
                        new FunctionParamModel
                        {
                            ParameterType = "unsigned __int16", Name = "__param4",
                            TypeReference = new TypeReference { IsPointer = true }
                        },
                        new FunctionParamModel
                        {
                            ParameterType = "unsigned __int16", Name = "__param5",
                            TypeReference = new TypeReference { IsPointer = true }
                        },
                        new FunctionParamModel
                        {
                            ParameterType = "void", Name = "__param6",
                            TypeReference = new TypeReference { IsPointer = true }
                        },
                        new FunctionParamModel
                        {
                            ParameterType = "void", Name = "__param7",
                            TypeReference = new TypeReference { IsPointer = true }
                        },
                        new FunctionParamModel
                        {
                            ParameterType = "void", Name = "__param8",
                            TypeReference = new TypeReference { IsPointer = true }
                        }
                    }
                }
            },
            new StructMemberModel
            {
                Name = "Reserved8", TypeString = "void", TypeReference = new TypeReference { IsPointer = true },
                IsFunctionPointer = false
            },
            new StructMemberModel
            {
                Name = "QuerySecurityContextToken",
                TypeString = "int",
                IsFunctionPointer = true,
                FunctionSignature = new FunctionSignatureModel
                {
                    ReturnTypeReference = new TypeReference { TypeString = "int" },
                    CallingConvention = "__stdcall",
                    Parameters = new List<FunctionParamModel>
                    {
                        new FunctionParamModel
                        {
                            ParameterType = "_SecHandle",
                            Name = "__param1",
                            TypeReference = new TypeReference { IsPointer = true }
                        },
                        new FunctionParamModel
                        {
                            ParameterType = "void",
                            Name = "__param2",
                            TypeReference = new TypeReference { IsPointer = true }
                        }
                    }
                }
            }
        };

        // Act & Assert
        Assert.Equal("_SECURITY_FUNCTION_TABLE_W", structModel.Name);
        Assert.Equal("_SECURITY_FUNCTION_TABLE_W", structModel.FullyQualifiedName);
        Assert.Equal(string.Empty, structModel.Namespace);
        Assert.Equal(TypeType.Struct, structModel.Type);

        Assert.Equal(6, members.Count);
        Assert.Equal("dwVersion", members[0].Name);
        Assert.Equal("unsigned int", members[0].TypeString);
        Assert.False(members[0].IsFunctionPointer); // Regular member should not be a function pointer

        Assert.Equal("QuerySecurityPackageInfoW", members[1].Name);
        Assert.Equal("int", members[1].TypeString);
        Assert.True(members[1].IsFunctionPointer);
        Assert.Equal("int", members[1].FunctionSignature!.ReturnTypeReference?.TypeString ?? string.Empty);
        Assert.Equal("__stdcall", members[1].FunctionSignature!.CallingConvention);
        Assert.Equal(2, members[1].FunctionSignature!.Parameters.Count);

        Assert.Equal("Reserved3", members[2].Name);
        Assert.Equal("void", members[2].TypeString);
        Assert.True(members[2].TypeReference
            ?.IsPointer ?? false); // Pointer type should have asterisk removed and IsPointer set to true
        Assert.False(members[2].IsFunctionPointer); // Regular member should not be a function pointer

        Assert.Equal("AddCredentialsW", members[3].Name);
        Assert.Equal("int", members[3].TypeString);
        Assert.True(members[3].IsFunctionPointer);
        Assert.Equal("int", members[3].FunctionSignature!.ReturnTypeReference?.TypeString ?? string.Empty);
        Assert.Equal("__stdcall", members[3].FunctionSignature!.CallingConvention);
        Assert.Equal(8, members[3].FunctionSignature!.Parameters.Count);

        Assert.Equal("Reserved8", members[4].Name);
        Assert.Equal("void", members[4].TypeString);
        Assert.True(members[4].TypeReference
            ?.IsPointer ?? false); // Pointer type should have asterisk removed and IsPointer set to true
        Assert.False(members[4].IsFunctionPointer); // Regular member should not be a function pointer

        Assert.Equal("QuerySecurityContextToken", members[5].Name);
        Assert.Equal("int", members[5].TypeString);
        Assert.True(members[5].IsFunctionPointer);
        Assert.Equal("int", members[5].FunctionSignature!.ReturnTypeReference?.TypeString ?? string.Empty);
        Assert.Equal("__stdcall", members[5].FunctionSignature!.CallingConvention);
        Assert.Equal(2, members[5].FunctionSignature!.Parameters.Count);
    }

    [Fact]
    public void CanCreateStructWithFunctionPointerMembers()
    {
        // Arrange
        var structModel = new StructTypeModel
        {
            Name = "DBOCache_vtbl",
            Namespace = string.Empty
        };

        var members = new List<StructMemberModel>
        {
            new StructMemberModel
            {
                Name = "Next",
                TypeString = "_EH3_EXCEPTION_REGISTRATION",
                TypeReference = new TypeReference { IsPointer = true },
                IsFunctionPointer = false
            },
            new StructMemberModel
            {
                Name = "GetCollection",
                TypeString = "Collection",
                IsFunctionPointer = true,
                FunctionSignature = new FunctionSignatureModel
                {
                    ReturnTypeReference = new TypeReference { TypeString = "Collection *" },
                    CallingConvention = "__thiscall",
                    Parameters = new List<FunctionParamModel>
                    {
                        new FunctionParamModel
                        {
                            ParameterType = "DBOCache",
                            Name = "this",
                            TypeReference = new TypeReference { IsPointer = true }
                        },
                        new FunctionParamModel
                        {
                            ParameterType = "IDClass<_tagDataID,32,0>",
                            Name = "__param2"
                        }
                    }
                }
            },
            new StructMemberModel
            {
                Name = "SetCollection",
                TypeString = "bool",
                IsFunctionPointer = true,
                FunctionSignature = new FunctionSignatureModel
                {
                    ReturnTypeReference = new TypeReference { TypeString = "bool" },
                    CallingConvention = "__thiscall",
                    Parameters = new List<FunctionParamModel>
                    {
                        new FunctionParamModel
                        {
                            ParameterType = "DBOCache",
                            Name = "this",
                            TypeReference = new TypeReference { IsPointer = true }
                        },
                        new FunctionParamModel
                        {
                            ParameterType = "Collection",
                            Name = "__param2",
                            TypeReference = new TypeReference { IsPointer = true }
                        }
                    }
                }
            }
        };

        // Act & Assert
        Assert.Equal("DBOCache_vtbl", structModel.Name);
        Assert.Equal("DBOCache_vtbl", structModel.FullyQualifiedName);
        Assert.Equal(string.Empty, structModel.Namespace);
        Assert.Equal(TypeType.Struct, structModel.Type);

        Assert.Equal(3, members.Count);

        // First member: _EH3_EXCEPTION_REGISTRATION *Next;
        Assert.Equal("Next", members[0].Name);
        Assert.Equal("_EH3_EXCEPTION_REGISTRATION", members[0].TypeString);
        Assert.True(members[0].TypeReference?.IsPointer ?? false);
        Assert.False(members[0].IsFunctionPointer);

        // Second member: function pointer
        Assert.Equal("GetCollection", members[1].Name);
        Assert.Equal("Collection", members[1].TypeString); // For function pointers, the type string is the return type
        Assert.True(members[1].IsFunctionPointer);
        Assert.NotNull(members[1].FunctionSignature);
        Assert.Equal("Collection *", members[1].FunctionSignature!.ReturnTypeReference?.TypeString ?? string.Empty);
        Assert.True(members[1].FunctionSignature!.Parameters[0].TypeReference?.IsPointer ?? false);
        Assert.Equal("__thiscall", members[1].FunctionSignature!.CallingConvention);

        // Third member: function pointer
        Assert.Equal("SetCollection", members[2].Name);
        Assert.Equal("bool", members[2].TypeString);
        Assert.True(members[2].IsFunctionPointer);
        Assert.Equal("bool", members[2].FunctionSignature!.ReturnTypeReference?.TypeString);
        Assert.Equal("__thiscall", members[2].FunctionSignature!.CallingConvention);
        Assert.Equal(2, members[2].FunctionSignature!.Parameters.Count);

        Assert.Equal("DBOCache", members[2].FunctionSignature!.Parameters[0].ParameterType);
        Assert.True(members[2].FunctionSignature!.Parameters[0].TypeReference?.IsPointer);
        Assert.Equal("this", members[2].FunctionSignature!.Parameters[0].Name);

        Assert.Equal("__param2", members[2].FunctionSignature!.Parameters[1].Name);
        Assert.Equal("Collection", members[2].FunctionSignature!.Parameters[1].ParameterType);
        Assert.True(members[2].FunctionSignature!.Parameters[1].TypeReference?.IsPointer);
    }

    #region MakeTypeModel Tests

    [Fact]
    public void MakeTypeModel_WithTemplateArguments_PopulatesTemplateArguments()
    {
        // Arrange
        var structModel = new StructTypeModel
        {
            Name = "SmartArray",
            Namespace = string.Empty,
            Source = "struct SmartArray<T,N> { };",
            TemplateArguments = new List<TypeReference>
            {
                new TypeReference { TypeString = "ContextMenuData" },
                new TypeReference { TypeString = "1" }
            }
        };

        // Act
        var typeModel = structModel.MakeTypeModel();

        // Assert
        Assert.Equal("SmartArray", typeModel.BaseName);
        Assert.Equal(string.Empty, typeModel.Namespace);
        Assert.Equal(TypeType.Struct, typeModel.Type);
        Assert.True(typeModel.IsGeneric);
        Assert.Equal(2, typeModel.TemplateArguments.Count);

        // Check first template argument
        Assert.Equal(0, typeModel.TemplateArguments[0].Position);
        Assert.Equal("ContextMenuData", typeModel.TemplateArguments[0].TypeString);

        // Check second template argument
        Assert.Equal(1, typeModel.TemplateArguments[1].Position);
        Assert.Equal("1", typeModel.TemplateArguments[1].TypeString);
    }

    [Fact]
    public void MakeTypeModel_WithBaseTypes_PopulatesBaseTypes()
    {
        // Arrange
        var structModel = new StructTypeModel
        {
            Name = "DerivedClass",
            Namespace = "TestNamespace",
            Source = "struct DerivedClass : public BaseClass1, public BaseClass2 { };",
            BaseTypes = new List<string>
            {
                "BaseClass1",
                "BaseClass2"
            }
        };

        // Act
        var typeModel = structModel.MakeTypeModel();

        // Assert
        Assert.Equal("DerivedClass", typeModel.BaseName);
        Assert.Equal("TestNamespace", typeModel.Namespace);
        Assert.Equal(2, typeModel.BaseTypes.Count);

        // Check first base type
        Assert.Equal(0, typeModel.BaseTypes[0].Order);
        Assert.Equal("BaseClass1", typeModel.BaseTypes[0].RelatedTypeString);
        Assert.Null(typeModel.BaseTypes[0].RelatedType); // Will be resolved later

        // Check second base type
        Assert.Equal(1, typeModel.BaseTypes[1].Order);
        Assert.Equal("BaseClass2", typeModel.BaseTypes[1].RelatedTypeString);
        Assert.Null(typeModel.BaseTypes[1].RelatedType); // Will be resolved later
    }

    [Fact]
    public void MakeTypeModel_WithTemplateArgumentsAndBaseTypes_PopulatesBoth()
    {
        // Arrange
        var structModel = new StructTypeModel
        {
            Name = "Container",
            Namespace = "Collections",
            Source = "struct Container<T> : public Base { };",
            TemplateArguments = new List<TypeReference>
            {
                new TypeReference { TypeString = "ItemType" }
            },
            BaseTypes = new List<string>
            {
                "Base"
            }
        };

        // Act
        var typeModel = structModel.MakeTypeModel();

        // Assert
        Assert.Equal("Container", typeModel.BaseName);
        Assert.Equal("Collections", typeModel.Namespace);

        // Verify template arguments
        Assert.True(typeModel.IsGeneric);
        Assert.Single(typeModel.TemplateArguments);
        Assert.Equal(0, typeModel.TemplateArguments[0].Position);
        Assert.Equal("ItemType", typeModel.TemplateArguments[0].TypeString);

        // Verify base types
        Assert.Single(typeModel.BaseTypes);
        Assert.Equal(0, typeModel.BaseTypes[0].Order);
        Assert.Equal("Base", typeModel.BaseTypes[0].RelatedTypeString);

        // Verify the NameWithTemplates property uses the template arguments
        Assert.Equal("Container<ItemType>", typeModel.NameWithTemplates);
    }

    [Fact]
    public void MakeTypeModel_WithNamespaceAndTemplateArguments_GeneratesCorrectFQN()
    {
        // Arrange
        var structModel = new StructTypeModel
        {
            Name = "Vector",
            Namespace = "std",
            Source = "struct Vector<T> { };",
            TemplateArguments = new List<TypeReference>
            {
                new TypeReference { TypeString = "int" }
            }
        };

        // Act
        var typeModel = structModel.MakeTypeModel();

        // Assert
        Assert.Equal("Vector<int>", typeModel.NameWithTemplates);
        Assert.Equal("std::Vector<int>", typeModel.FullyQualifiedName);
    }

    [Fact]
    public void MakeTypeModel_WithNoTemplateArgumentsOrBaseTypes_PreservesOtherProperties()
    {
        // Arrange
        var structModel = new StructTypeModel
        {
            Name = "SimpleClass",
            Namespace = "MyNamespace",
            Source = "struct SimpleClass { };"
        };

        // Act
        var typeModel = structModel.MakeTypeModel();

        // Assert
        Assert.Equal("SimpleClass", typeModel.BaseName);
        Assert.Equal("MyNamespace", typeModel.Namespace);
        Assert.Equal(TypeType.Struct, typeModel.Type);
        Assert.Equal("struct SimpleClass { };", typeModel.Source);
        Assert.False(typeModel.IsGeneric);
        Assert.Empty(typeModel.TemplateArguments);
        Assert.Empty(typeModel.BaseTypes);
    }

    [Fact]
    public void MakeTypeModel_WithMultipleBaseTypesAndTemplates_MaintainsOrder()
    {
        // Arrange
        var structModel = new StructTypeModel
        {
            Name = "ComplexType",
            Namespace = "Advanced",
            TemplateArguments = new List<TypeReference>
            {
                new TypeReference { TypeString = "K" },
                new TypeReference { TypeString = "V" },
                new TypeReference { TypeString = "C" }
            },
            BaseTypes = new List<string>
            {
                "Base1",
                "Base2",
                "Base3"
            }
        };

        // Act
        var typeModel = structModel.MakeTypeModel();

        // Assert
        // Verify template argument order
        Assert.Equal(3, typeModel.TemplateArguments.Count);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(i, typeModel.TemplateArguments[i].Position);
        }

        // Verify base type order
        Assert.Equal(3, typeModel.BaseTypes.Count);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(i, typeModel.BaseTypes[i].Order);
            Assert.Equal($"Base{i + 1}", typeModel.BaseTypes[i].RelatedTypeString);
        }
    }

    #endregion
}
