using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Services;
using ACDecompileParser.Shared.Lib.Storage;
using Moq;
using Xunit;

namespace ACDecompileParser.Tests.Lib.Parser;

public class TypeGroupingTests
{
  [Fact]
  public void Parse_SiblingStructs_ShouldNotBeNested()
  {
    // Arrange
    // Note: The example provided by the user shows UIElement_Text defined first, then UIElement_Button.
    // The parser should treat them as siblings unless one is explicitly nested (which they are not in the C++ example).
    var source = @"
/* 123 */
enum UIElement_Text::AddTextFlags
{
  atf_Default = 0x0,
  atf_PerserveSelection = 0x1,
  atf_AppendText = 0x2,
};
/* 1237 */
struct __declspec(align(8)) UIElement_Text
{
  UIElement_Scrollable baseclass_0;
  CInputHandler baseclass_608;
  GlyphList m_glyphList;
};
/* 12337 */
struct __declspec(align(8)) UIElement_Button
{
  UIElement_Text baseclass_0;
};
";
    var parser = new SourceParser(new List<string> { source });
    parser.Parse();

    // Act
    // Verify parsing models

    var uiText = parser.TypeModels.FirstOrDefault(t => t.BaseName == "UIElement_Text");
    var uiButton = parser.TypeModels.FirstOrDefault(t => t.BaseName == "UIElement_Button");
    var addTextFlags = parser.TypeModels.FirstOrDefault(t => t.BaseName == "AddTextFlags");

    // Populate BaseTypePaths (this determines file grouping)
    var repoMock = new Mock<ITypeRepository>();

    // Setup mock to update the property on the object when UpdateBaseTypePath is called
    repoMock.Setup(r => r.UpdateBaseTypePath(It.IsAny<int>(), It.IsAny<string>()))
      .Callback<int, string>((id, path) =>
      {
        var t = parser.TypeModels.FirstOrDefault(m => m.Id == id);
        if (t != null) t.BaseTypePath = path;
      });

    // We need to ensure IDs are set so the callback works (SourceParser sets them? No, usually DB sets IDs. SourceParser sets indexes maybe?)
    // SourceParser doesn't set IDs. They are 0.
    // So we need to manually assign IDs for the validation to work with the mock callback lookup 
    // OR we can just check the arguments passed to Verify.
    int idCounter = 1;
    foreach (var t in parser.TypeModels) t.Id = idCounter++;

    var resolutionService = new TypeResolutionService(repoMock.Object);
    resolutionService.PopulateBaseTypePaths(parser.TypeModels);

    // Check Namespace
    Assert.Equal("", uiText.Namespace);
    Assert.Equal("", uiButton.Namespace);
    Assert.Equal("UIElement_Text", addTextFlags.Namespace);

    // Check BaseTypePath - THIS is what controls file grouping
    // UIElement_Text should be its own group (or empty BaseTypePath implies BaseName)
    // AddTextFlags should have BaseTypePath pointing to UIElement_Text
    Assert.Equal("UIElement_Text", addTextFlags.BaseTypePath);

    // UIElement_Button should NOT have BaseTypePath pointing to UIElement_Text
    Assert.NotEqual("UIElement_Text", uiButton.BaseTypePath);

    // DEBUG: Output the FQN of uiText and Namespace of addTextFlags
    // This helps diagnosis if LinkNestedTypes fails
    Assert.Equal("UIElement_Text", uiText.StoredFullyQualifiedName);
    Assert.Equal("UIElement_Text", addTextFlags.Namespace);

    // Now let's check TypeHierarchyService logic for nesting (C# class nesting)
    var hierarchyService = new TypeHierarchyService();
    var types = parser.TypeModels;
    hierarchyService.LinkNestedTypes(types);

    // UIElement_Button should NOT be a nested type of UIElement_Text
    Assert.Null(uiButton.ParentType);
    Assert.DoesNotContain(uiButton, uiText.NestedTypes ?? new List<TypeModel>());

    // AddTextFlags SHOULD be a nested type of UIElement_Text
    Assert.Equal(uiText, addTextFlags.ParentType);
    Assert.Contains(addTextFlags, uiText.NestedTypes ?? new List<TypeModel>());
  }

  [Fact]
  public void Parse_StructWithDeclspec_ShouldHaveCorrectName()
  {
    var source = @"/* 1 */
struct __declspec(align(8)) UIElement_Text { };";
    var parser = new SourceParser(new List<string> { source });
    parser.Parse();

    var model = parser.TypeModels.FirstOrDefault();
    Assert.NotNull(model);
    Assert.Equal("UIElement_Text", model.BaseName);
    Assert.Equal("", model.Namespace);
  }

  [Fact]
  public void Parse_StructWithUnusualDeclspec_ShouldHaveCorrectName()
  {
    // Test with a declspec that the regex might NOT match (e.g. no align)
    var source = @"/* 2 */
struct __declspec(dllimport) UIElement_Imported { };";
    var parser = new SourceParser(new List<string> { source });
    parser.Parse();

    var model = parser.TypeModels.FirstOrDefault();
    Assert.NotNull(model);
    // If regex fails, this might be "__declspec" or "dllimport"
    // Wait, if regex fails, EatType takes "__declspec" as type? 
    // Or if it parses "__declspec(dllimport)", EatType stops at '('.
    // So name is "__declspec".
    // Let's assert what we EXPECT (which might be the bug behavior)

    // If CleanDefinitionLine fails to remove dllimport, EatType sees "__declspec(dllimport) UIElement_Imported".
    // EatType parses "__declspec".
    // So BaseName is "__declspec". 
    // Namespace is empty.

    // BUT if it works correctly, it should be UIElement_Imported.

    // Let's assert "UIElement_Imported" and see if it fails.
    Assert.Equal("UIElement_Imported", model.BaseName);
    Assert.Equal("", model.Namespace);
  }
}
