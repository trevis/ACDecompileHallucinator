using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Lib.Parser;
using TypeParser = ACDecompileParser.Lib.Parser.TypeParser;

namespace ACDecompileParser.Tests.Lib.Parser;

public class TypeParserTests
{
    [Fact]
    public void EatType_HandlesMultiNamespaceWithCompilerNames()
    {
        // Arrange
        var defLine = "____::$2B1A4197686722557092EC31::$20AEC::$0BCM::$0A::$ComputeMinMaxCurve";
    
        // Act
        var remaining = TypeParser.EatType(defLine, out var typeStr);
    
        // Assert
        Assert.Equal("____::$2B1A4197686722557092EC31::$20AEC::$0BCM::$0A::$ComputeMinMaxCurve", typeStr);
        Assert.Equal("", remaining);
    }
    
    [Fact]
    public void EatType_HandlesNestedTemplatesWithoutPointer()
    {
        // Arrange
        var defLine = "AutoGrowHashTable<unsigned long,AutoGrowHashTable<unsigned long,SmartArray<UIMessageData,1> > > m_elementListenerTable;";
    
        // Act
        var remaining = TypeParser.EatType(defLine, out var typeStr);
    
        // Assert
        Assert.Equal("AutoGrowHashTable<unsigned long,AutoGrowHashTable<unsigned long,SmartArray<UIMessageData,1>>>", typeStr);
        Assert.Equal("m_elementListenerTable;", remaining);
    }
    
    [Fact]
    public void EatType_HandlesNestedTemplates()
    {
        // Arrange
        var defLine = "IntrusiveHashTable<unsigned long,HashTableData<unsigned long,CaseInsensitiveStringBase<PStringBase<char> > > *,1> *m_currHashTable;";
        
        // Act
        var remaining = TypeParser.EatType(defLine, out var typeStr);
        
        // Assert
        Assert.Equal("IntrusiveHashTable<unsigned long,HashTableData<unsigned long,CaseInsensitiveStringBase<PStringBase<char>>>*,1>*", typeStr);
        Assert.Equal("m_currHashTable;", remaining);
    }
    
    [Fact]
    public void EatType_HandlesConstPointer()
    {
        // Arrange
        var defLine = "const MasterProperty *m_object;";
        
        // Act
        var remaining = TypeParser.EatType(defLine, out var typeStr);
        
        // Assert
        Assert.Equal("const MasterProperty*", typeStr);
        Assert.Equal("m_object;", remaining);
    }
    
    [Fact]
    public void EatType_HandlesTemplateWithoutVariable()
    {
        // Arrange
        var defLine = "HashTable<unsigned long,BasePropertyDesc *,0>";
        
        // Act
        var remaining = TypeParser.EatType(defLine, out var typeStr);
        
        // Assert
        Assert.Equal("HashTable<unsigned long,BasePropertyDesc*,0>", typeStr);
        Assert.Equal("", remaining);
    }
    
    [Fact]
    public void EatType_HandlesInheritance()
    {
        // Arrange
        var defLine = "UIElementManager : CInputHandler, IInputActionCallback";
        
        // Act
        var remaining = TypeParser.EatType(defLine, out var typeStr);
        
        // Assert
        Assert.Equal("UIElementManager", typeStr);
        Assert.Equal(": CInputHandler, IInputActionCallback", remaining);
    }
    
    [Fact]
    public void EatType_HandlesEmptyString()
    {
        // Arrange
        var defLine = "";
        
        // Act
        var remaining = TypeParser.EatType(defLine, out var typeStr);
        
        // Assert
        Assert.Equal("", typeStr);
        Assert.Equal("", remaining);
    }
    
    [Fact]
    public void EatType_HandlesWhitespaceOnly()
    {
        // Arrange
        var defLine = "   ";
        
        // Act
        var remaining = TypeParser.EatType(defLine, out var typeStr);
        
        // Assert
        Assert.Equal("", typeStr);
        Assert.Equal("", remaining);
    }
    
    [Fact]
    public void EatType_HandlesSimpleType()
    {
        // Arrange
        var defLine = "int myVariable;";
        
        // Act
        var remaining = TypeParser.EatType(defLine, out var typeStr);
        
        // Assert
        Assert.Equal("int", typeStr);
        Assert.Equal("myVariable;", remaining);
    }
    
    [Fact]
    public void EatType_HandlesPointerWithSpaces()
    {
        // Arrange
        var defLine = "char * myString;";
        
        // Act
        var remaining = TypeParser.EatType(defLine, out var typeStr);
        
        // Assert
        Assert.Equal("char*", typeStr);
        Assert.Equal("myString;", remaining);
    }
    
    [Fact]
    public void EatType_HandlesReference()
    {
        // Arrange
        var defLine = "std::string& myRef;";
        
        // Act
        var remaining = TypeParser.EatType(defLine, out var typeStr);
        
        // Assert
        Assert.Equal("std::string&", typeStr);
        Assert.Equal("myRef;", remaining);
    }
    
    [Fact]
    public void EatType_HandlesDoublePointer()
    {
        // Arrange
        var defLine = "char **argv;";
        
        // Act
        var remaining = TypeParser.EatType(defLine, out var typeStr);
        
        // Assert
        Assert.Equal("char**", typeStr);
        Assert.Equal("argv;", remaining);
    }
}