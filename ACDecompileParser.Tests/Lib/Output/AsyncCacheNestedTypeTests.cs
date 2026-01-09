using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Lib.Parser;
using Microsoft.EntityFrameworkCore;
using ACDecompileParser.Shared.Lib.Storage;
using System.IO;

namespace ACDecompileParser.Tests.Lib.Output;

public class AsyncCacheNestedTypeTests
{
    [Fact]
    public void ParseAndGenerateHeaderFiles_WithAsyncCacheExample_CreatesSingleFile()
    {
        // Arrange - Test the exact AsyncCache example from the issue
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 123 */",
                "struct __cppobj AsyncCache::CAsyncRequest::CCallbackWrapper",
                "{",
                " AsyncCache::CCallbackHandler *pCallbackHandler;",
                "  unsigned int dwTimesFinished;",
                "};",
                "",
                "/* 125 */",
                "struct __cppobj __declspec(align(4)) AsyncCache",
                "{",
                "  AsyncCache_vtbl *__vftable /*VFT*/;",
                "  bool m_bCallingPendingCallbacks;",
                "};",
                "",
                "/* 125 */",
                "struct __cppobj AsyncCache::CAsyncRequest : ReferenceCountTemplate<1048576,0>",
                "{",
                "  DBObj *m_pObj;",
                "};",
                "",
                "/* 125 */",
                "struct /*VFT*/ AsyncCache::CAsyncRequest_vtbl",
                "{",
                "  void (__thiscall *ReleaseDBObj)(AsyncCache::CAsyncRequest *this);",
                "};"
            }
        };
        var parser = new SourceParser(sourceFileContents);

        // Create a temporary database for testing
        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        using var repo = new SqlTypeRepository(context);

        // Create a temporary directory for header output
        string tempDir = Path.Combine(Path.GetTempPath(), "test_output_" + Guid.NewGuid().ToString());

        try
        {
            // Act
            parser.Parse();
            parser.SaveToDatabase(repo);
            parser.GenerateHeaderFiles(tempDir, repo);

            // Assert
            var allFiles = Directory.GetFiles(tempDir, "*.h", SearchOption.AllDirectories);

            // Should only be one file: AsyncCache.h (containing all related types)
            Assert.Single(allFiles);

            string headerFilePath = allFiles[0];
            Assert.Contains("AsyncCache.h", headerFilePath);

            // Verify the file contains all struct definitions
            // With nested type output, nested types are rendered inside their parent with short names
            string fileContent = File.ReadAllText(headerFilePath);
            Assert.Contains("struct AsyncCache", fileContent);

            // Nested types are rendered with their short BaseName inside the parent
            Assert.Contains("struct CAsyncRequest", fileContent); // Inside AsyncCache
            Assert.Contains("struct CCallbackWrapper", fileContent); // Inside CAsyncRequest  
            Assert.Contains("struct CAsyncRequest_vtbl", fileContent); // Inside AsyncCache
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void Parse_WithAsyncCacheExample_SetsBaseTypePathCorrectly()
    {
        // Arrange - Test the exact AsyncCache example from the issue
        var sourceFileContents = new List<List<string>>
        {
            new List<string>
            {
                "/* 123 */",
                "struct __cppobj AsyncCache::CAsyncRequest::CCallbackWrapper",
                "{",
                "  AsyncCache::CCallbackHandler *pCallbackHandler;",
                " unsigned int dwTimesFinished;",
                "};",
                "",
                "/* 125 */",
                "struct __cppobj __declspec(align(4)) AsyncCache",
                "{",
                "  AsyncCache_vtbl *__vftable /*VFT*/;",
                "  bool m_bCallingPendingCallbacks;",
                "};",
                "",
                "/* 125 */",
                "struct __cppobj AsyncCache::CAsyncRequest : ReferenceCountTemplate<1048576,0>",
                "{",
                " DBObj *m_pObj;",
                "};",
                "",
                "/* 125 */",
                "struct /*VFT*/ AsyncCache::CAsyncRequest_vtbl",
                "{",
                "  void (__thiscall *ReleaseDBObj)(AsyncCache::CAsyncRequest *this);",
                "};"
            }
        };
        var parser = new SourceParser(sourceFileContents);

        // Create a temporary database for testing
        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
        using var context = new TypeContext(optionsBuilder.Options);
        using var repo = new SqlTypeRepository(context);

        // Act
        parser.Parse();
        parser.SaveToDatabase(repo);

        // Assert
        var allTypes = repo.GetAllTypes();
        Assert.Equal(4, allTypes.Count); // 4 types: AsyncCache, CAsyncRequest, CCallbackWrapper, CAsyncRequest_vtbl

        // Find all the types
        var asyncCacheType = allTypes.FirstOrDefault(t => t.FullyQualifiedName == "AsyncCache");
        var asyncRequestType = allTypes.FirstOrDefault(t => t.FullyQualifiedName == "AsyncCache::CAsyncRequest");
        var callbackWrapperType =
            allTypes.FirstOrDefault(t => t.FullyQualifiedName == "AsyncCache::CAsyncRequest::CCallbackWrapper");
        var asyncRequestVtableType =
            allTypes.FirstOrDefault(t => t.FullyQualifiedName == "AsyncCache::CAsyncRequest_vtbl");

        Assert.NotNull(asyncCacheType);
        Assert.NotNull(asyncRequestType);
        Assert.NotNull(callbackWrapperType);
        Assert.NotNull(asyncRequestVtableType);

        // For the current (broken) implementation, this test will fail, showing the issue:
        // All nested types and vtables should group with the root AsyncCache type
        Assert.Equal("AsyncCache", asyncCacheType.BaseTypePath);
        Assert.Equal("AsyncCache", asyncRequestType.BaseTypePath);
        Assert.Equal("AsyncCache",
            callbackWrapperType.BaseTypePath); // This is the main issue - it's probably "AsyncCache::CAsyncRequest"
        Assert.Equal("AsyncCache", asyncRequestVtableType.BaseTypePath);
    }
}
