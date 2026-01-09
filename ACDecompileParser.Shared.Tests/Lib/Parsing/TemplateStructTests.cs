using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Models;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Parsing;

public class TemplateStructTests
{
    [Fact]
    public void VerifyDistinctTemplateInstantiationsAreNotNested()
    {
        string input = @"
/* 1978 */
struct __declspec(align(4)) IntrusiveHashTable<int,CAsyncStateHandler *,1>
{
  void *__vftable;
  CAsyncStateHandler *m_aInplaceBuckets[23];
  CAsyncStateHandler **m_buckets;
  CAsyncStateHandler **m_firstInterestingBucket;
  unsigned int m_numBuckets;
  unsigned int m_numElements;
};

/* 751 */
struct __declspec(align(4)) IntrusiveHashTable<unsigned long,HashTableData<unsigned long,StringInfoData *> *,0>
{
  void *__vftable;
  void *m_aInplaceBuckets[23];
  void *m_buckets;
  void *m_firstInterestingBucket;
  unsigned int m_numBuckets;
  unsigned int m_numElements;
};
";
        var parser = new SourceParser(new List<string> { input });
        parser.Parse();

        // Filter to only the IntrusiveHashTable types we care about
        var hashtables = parser.TypeModels
            .Where(t => t.BaseName == "IntrusiveHashTable")
            .ToList();

        // 1. Should have exactly 2
        Assert.Equal(2, hashtables.Count);

        // 2. Both should be top-level (no ParentType)
        foreach (var t in hashtables)
        {
            Assert.Null(t.ParentType);

            // Also check that neither contains any nested types, 
            // specifically checking that they don't contain each other.
            if (t.NestedTypes != null)
            {
                Assert.Empty(t.NestedTypes);
            }
        }

        // 3. Verify their full names to ensure they were parsed correctly as distinct templates
        var names = hashtables.Select(t => t.NameWithTemplates).ToList();
        var allNames = string.Join("; ", names);

        Assert.True(names.Contains("IntrusiveHashTable<int,CAsyncStateHandler*,1>"),
            $"Expected 'IntrusiveHashTable<int,CAsyncStateHandler*,1>' but found: {allNames}");
        Assert.True(names.Contains("IntrusiveHashTable<unsigned long,HashTableData<unsigned long,StringInfoData*>*,0>"),
            $"Expected 'IntrusiveHashTable<unsigned long,HashTableData<unsigned long,StringInfoData*>*,0>' but found: {allNames}");
    }
}
