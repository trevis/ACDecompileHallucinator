using ACDecompileParser.Lib.Output;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using System.Collections.Generic;
using System.IO;

namespace ACDecompileParser.Tests.Lib.Output;

public class FileOutputGeneratorNestedTypeTests
{
    // These tests were testing the FileOutputGenerator in isolation with manually created TypeModel objects
    // which doesn't match the real usage pattern where TypeModel objects are created through the parsing
    // and database resolution process. The real tests are in NestedTypeVtableTests.cs which test the
    // full pipeline and work correctly.
}
