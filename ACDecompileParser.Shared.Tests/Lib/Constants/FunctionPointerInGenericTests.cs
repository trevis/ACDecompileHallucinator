using System;
using ACDecompileParser.Shared.Lib.Constants;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Constants
{
    public class FunctionPointerInGenericTests
    {
        [Fact]
        public void Test_FunctionPointer_Inside_Template_Arguments()
        {
            // Input: HashTable<unsigned long,UIElement * (__cdecl*)(LayoutDesc const &,ElementDesc const &),0>
            string input = "HashTable<unsigned long,UIElement * (__cdecl*)(LayoutDesc const &,ElementDesc const &),0>";

            // Expected: ACBindings.HashTable<uint,System.IntPtr>
            // Explanation:
            // 1. unsigned long -> uint
            // 2. UIElement * (__cdecl*)(...) -> System.IntPtr (function pointer mapped to IntPtr)
            // 3. 0 -> ignored (numeric literal)

            string result = PrimitiveTypeMappings.MapType(input);
            Assert.Equal("ACBindings.HashTable__uint__void_ptr", result);
        }
    }
}
