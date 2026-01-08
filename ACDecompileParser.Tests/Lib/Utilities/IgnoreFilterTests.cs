using Xunit;
using ACDecompileParser.Shared.Lib.Utilities;

namespace ACDecompileParser.Tests.Lib.Utilities;

public class IgnoreFilterTests
{
    [Fact]
    public void ShouldIgnoreType_PrefixMatch_ReturnsTrue()
    {
        // Arrange
        string typeName = "_SomeStruct";
        
        // Act
        bool result = IgnoreFilter.ShouldIgnoreType(typeName);
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public void ShouldIgnoreType_SuffixMatch_ReturnsTrue()
    {
        // Arrange
        string typeName = "SomeStruct_";
        
        // Act
        bool result = IgnoreFilter.ShouldIgnoreType(typeName);
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public void ShouldIgnoreType_Whitelisted_ReturnsFalse()
    {
        // Arrange
        string typeName = "_IDClass";
        
        // Act
        bool result = IgnoreFilter.ShouldIgnoreType(typeName);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public void ShouldIgnoreType_NamespacePrefixMatch_ReturnsTrue()
    {
        // Arrange
        string typeName = "_STL::_SomeStruct";
        
        // Act
        bool result = IgnoreFilter.ShouldIgnoreType(typeName);
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public void ShouldIgnoreType_NamespaceSuffixMatch_ReturnsTrue()
    {
        // Arrange
        string typeName = "NS::SomeStruct_tag";
        
        // Act
        bool result = IgnoreFilter.ShouldIgnoreType(typeName);
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public void ShouldIgnoreType_ValidType_ReturnsFalse()
    {
        // Arrange
        string typeName = "ValidStruct";
        
        // Act
        bool result = IgnoreFilter.ShouldIgnoreType(typeName);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public void ShouldIgnoreType_WhitelistedWithNamespace_ReturnsFalse()
    {
        // Arrange
        string typeName = "NS::_IDClass";
        
        // Act
        bool result = IgnoreFilter.ShouldIgnoreType(typeName);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public void ShouldIgnoreType_TemplateWithPrefix_ReturnsTrue()
    {
        // Arrange
        string typeName = "_STL::tagSomeTemplate<int>";
        
        // Act
        bool result = IgnoreFilter.ShouldIgnoreType(typeName);
        
        // Assert
        Assert.True(result);
    }
}
