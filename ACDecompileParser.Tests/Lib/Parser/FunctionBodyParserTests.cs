using ACDecompileParser.Lib.Parser;

namespace ACDecompileParser.Tests.Lib.Parser;

public class FunctionBodyParserTests
{
    [Fact]
    public void Parse_GlobalFunction_ReturnsCorrectModel()
    {
        var input = new List<string>
        {
            "//----- (006CF720) --------------------------------------------------------",
            "int _E1281_0()",
            "{",
            "  PStringBase<char>::PStringBase<char>(&ScaleY_0, \"ScaleY\");",
            "  return atexit(_E1282_6);",
            "}"
        };

        var results = FunctionBodyParser.Parse(input);

        Assert.Single(results);
        var func = results[0];
        Assert.Equal("_E1281_0", func.FullyQualifiedName);
        Assert.NotNull(func.FunctionSignature);
        Assert.Equal("_E1281_0", func.FunctionSignature!.Name);
        Assert.Equal("int", func.FunctionSignature!.ReturnType);
        Assert.Empty(func.FunctionSignature!.Parameters);
        Assert.Contains("return atexit(_E1282_6);", func.BodyText);
    }

    [Fact]
    public void Parse_MemberFunction_ReturnsCorrectModel()
    {
        var input = new List<string>
        {
            "//----- (00559090) --------------------------------------------------------",
            "int __thiscall ClientObjMaintSystem::Handle_Qualities__PrivateUpdateString(ClientObjMaintSystem *this, char wts, unsigned int stype, AC1Legacy::PStringBase<char> *val)",
            "{",
            "  unsigned int v4; // eax",
            "",
            "  if ( SmartBox::smartbox )",
            "    v4 = SmartBox::smartbox->player_id;",
            "  else",
            "    v4 = 0;",
            "  return ClientObjMaintSystem::UpdateStat<String_QualityType,::PStringBase<char> const &,signed char,::PStringBase<char> const &,__segment,egacy>(",
            "           (int)this,",
            "           stype,",
            "           wts,",
            "           v4,",
            "           val);",
            "}"
        };

        var results = FunctionBodyParser.Parse(input);

        Assert.Single(results);
        var func = results[0];
        Assert.Equal(
            "ClientObjMaintSystem::Handle_Qualities__PrivateUpdateString",
            func.FullyQualifiedName);
        Assert.NotNull(func.FunctionSignature);
        Assert.Equal("ClientObjMaintSystem::Handle_Qualities__PrivateUpdateString", func.FunctionSignature!.Name);
        Assert.Equal("int", func.FunctionSignature!.ReturnType);
        Assert.Equal("__thiscall", func.FunctionSignature!.CallingConvention);
        Assert.Equal(4, func.FunctionSignature!.Parameters.Count);
        Assert.Equal("this", func.FunctionSignature!.Parameters[0].Name);
        Assert.Equal("ClientObjMaintSystem*", func.FunctionSignature!.Parameters[0].ParameterType);
        Assert.Contains("SmartBox::smartbox", func.BodyText);
    }

    [Fact]
    public void Parse_ComplexTemplateParams_ReturnsCorrectModel()
    {
        var input = new List<string>
        {
            "//----- (005590C0) --------------------------------------------------------",
            "int __thiscall ClientObjMaintSystem::Handle_Qualities__PrivateUpdateDataID(ClientObjMaintSystem *this, char wts, unsigned int stype, IDClass<_tagDataID,32,0> val)",
            "{",
            "  return 0;",
            "}"
        };

        var results = FunctionBodyParser.Parse(input);

        Assert.Single(results);
        var func = results[0];
        Assert.Equal(
            "ClientObjMaintSystem::Handle_Qualities__PrivateUpdateDataID",
            func.FullyQualifiedName);
        Assert.Equal(4, func.FunctionSignature!.Parameters.Count);
        Assert.Equal("val", func.FunctionSignature!.Parameters[3].Name);
        Assert.Equal("IDClass<_tagDataID,32,0>", func.FunctionSignature!.Parameters[3].ParameterType);
    }

    [Fact]
    public void Parse_HandlesFunctionPointerInParameters()
    {
        var lines = new List<string>
        {
            "//----- (0045B950) --------------------------------------------------------",
            "void __thiscall IntrusiveHashTable<unsigned long,HashTableData<unsigned long,UIElement * (__cdecl *)(LayoutDesc const &,ElementDesc const &)> *,0>::IntrusiveHashTable<unsigned long,HashTableData<unsigned long,UIElement * (__cdecl *)(LayoutDesc const &,ElementDesc const &)> *,0>(IntrusiveHashTable<unsigned long,HashTableData<unsigned long,UIElement * (__cdecl *)(LayoutDesc const &,ElementDesc const &)> *,0> *this, unsigned int _numBuckets)",
            "{",
            "  // Body",
            "}"
        };

        var results = FunctionBodyParser.Parse(lines);

        Assert.Single(results);
        var func = results[0];
        Assert.NotNull(func.FunctionSignature);
        // The first parameter should include the full complex type (FunctionParamParser normalizes spaces)
        // Expected: "UIElement* (__cdecl*)(LayoutDesc const&,ElementDesc const&)"
        var type = func.FunctionSignature.Parameters[0].ParameterType;
        Assert.Contains("UIElement*", type);
        Assert.Contains("(__cdecl*)", type);
        Assert.Contains("LayoutDesc const&", type);
        Assert.Contains("ElementDesc const&", type);
    }

    [Fact]
    public void Parse_MultiLineSignature_ReturnsCorrectModel()
    {
        var input = new List<string>
        {
            "//----- (00682570) --------------------------------------------------------",
            "bool __thiscall TabooTable::CreateCheckString(",
            "        TabooTable *this,",
            "        unsigned int chkType,",
            "        const char *baseStr,",
            "        unsigned int baseStrLength,",
            "        char *strOut)",
            "{",
            "  return 1;",
            "}"
        };

        var results = FunctionBodyParser.Parse(input);

        Assert.Single(results);
        var func = results[0];
        Assert.Equal(
            "TabooTable::CreateCheckString",
            func.FullyQualifiedName);
        Assert.Equal(5, func.FunctionSignature!.Parameters.Count);
        Assert.Equal("strOut", func.FunctionSignature!.Parameters[4].Name);
    }

    [Fact]
    public void Parse_ErrorDirective_ShouldSkip()
    {
        var input = new List<string>
        {
            "//----- (005571C0) --------------------------------------------------------",
            "#error \"557292: positive sp value has been found (funcsize=131)\""
        };

        var results = FunctionBodyParser.Parse(input);

        Assert.Empty(results);
    }
}
