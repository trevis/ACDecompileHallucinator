using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Output.Rules;
using ACDecompileParser.Shared.Lib.Services;
using Moq;
using System.Text.RegularExpressions;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Output;

public class FqnMatchRuleTests
{
    private readonly Mock<IInheritanceGraph> _mockGraph;

    public FqnMatchRuleTests()
    {
        _mockGraph = new Mock<IInheritanceGraph>();
    }

    [Fact]
    public void Matches_ExactFqnMatch_ReturnsTrue()
    {
        // Arrange
        var rule = new FqnMatchRule(@"^MyNamespace::MyClass$", "Output/Path/");
        var type = new TypeModel
        {
            Namespace = "MyNamespace",
            BaseName = "MyClass"
        };

        // Act
        var result = rule.Matches(type, _mockGraph.Object, out var hierarchyResult);

        // Assert
        Assert.True(result);
        Assert.NotNull(hierarchyResult);
        Assert.Equal("Output/Path/", hierarchyResult.Prefix);
    }

    [Fact]
    public void Matches_ExactFqnNoMatch_ReturnsFalse()
    {
        // Arrange
        var rule = new FqnMatchRule(@"^MyNamespace::MyClass$", "Output/Path/");
        var type = new TypeModel
        {
            Namespace = "OtherNamespace",
            BaseName = "OtherClass"
        };

        // Act
        var result = rule.Matches(type, _mockGraph.Object, out var hierarchyResult);

        // Assert
        Assert.False(result);
        Assert.Null(hierarchyResult);
    }

    [Fact]
    public void Matches_RegexPatternMatch_ReturnsTrue()
    {
        // Arrange
        var rule = new FqnMatchRule(@"^MyNamespace::.*$", "Output/Path/");
        var type = new TypeModel
        {
            Namespace = "MyNamespace",
            BaseName = "MyClass"
        };

        // Act
        var result = rule.Matches(type, _mockGraph.Object, out var hierarchyResult);

        // Assert
        Assert.True(result);
        Assert.NotNull(hierarchyResult);
        Assert.Equal("Output/Path/", hierarchyResult.Prefix);
    }

    [Fact]
    public void Matches_RegexPatternNoMatch_ReturnsFalse()
    {
        // Arrange
        var rule = new FqnMatchRule(@"^MyNamespace::.*$", "Output/Path/");
        var type = new TypeModel
        {
            Namespace = "OtherNamespace",
            BaseName = "MyClass"
        };

        // Act
        var result = rule.Matches(type, _mockGraph.Object, out var hierarchyResult);

        // Assert
        Assert.False(result);
        Assert.Null(hierarchyResult);
    }

    [Fact]
    public void Matches_SingleCharPatternMatch_ReturnsTrue()
    {
        // Arrange
        var rule = new FqnMatchRule(@"^MyNamespace::MyCl.ss$", "Output/Path/");
        var type = new TypeModel
        {
            Namespace = "MyNamespace",
            BaseName = "MyClass"
        };

        var actualFqn = type.FullyQualifiedName;
        
        // Check if regex matches manually
        var testRegex = new Regex(@"^MyNamespace::MyCl.s$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var manualMatch = testRegex.IsMatch(actualFqn);

        // Act
        var result = rule.Matches(type, _mockGraph.Object, out var hierarchyResult);

        // Assert
        Assert.True(result, $"Expected match but got no match. FQN: '{actualFqn}', Pattern: '^MyNamespace::MyCl.s$'");
        Assert.NotNull(hierarchyResult);
        Assert.Equal("Output/Path/", hierarchyResult.Prefix);
    }

    [Fact]
    public void Matches_CaseInsensitiveMatch_ReturnsTrue()
    {
        // Arrange
        var rule = new FqnMatchRule(@"^mynamespace::myclass$", "Output/Path/");
        var type = new TypeModel
        {
            Namespace = "MyNamespace",
            BaseName = "MyClass"
        };

        // Act
        var result = rule.Matches(type, _mockGraph.Object, out var hierarchyResult);

        // Assert
        Assert.True(result);
        Assert.NotNull(hierarchyResult);
        Assert.Equal("Output/Path/", hierarchyResult.Prefix);
    }

    [Fact]
    public void Matches_FqnWithTemplateArgs_MatchesCorrectly()
    {
        // Arrange
        var rule = new FqnMatchRule(@"^MyNamespace::Vector<int>$", "Output/Path/");
        var type = new TypeModel
        {
            Namespace = "MyNamespace",
            BaseName = "Vector",
            TemplateArguments = new List<TypeTemplateArgument>
            {
                new TypeTemplateArgument
                {
                    Position = 0,
                    TypeString = "int"
                }
            }
        };

        // The FullyQualifiedName property should include template args
        Assert.Equal("MyNamespace::Vector<int>", type.FullyQualifiedName);
        
        // Act
        var result = rule.Matches(type, _mockGraph.Object, out var hierarchyResult);

        // Assert
        Assert.True(result);
        Assert.NotNull(hierarchyResult);
        Assert.Equal("Output/Path/", hierarchyResult.Prefix);
    }
}
