using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using Moq;
using Xunit;
using ACDecompileParser.Shared.Lib.Storage;

namespace ACDecompileParser.Tests.Lib.Output
{
    public class StaticMemberReproductionTest
    {
        [Fact]
        public void Generate_ClassWithStaticMembers_OutputsStaticMembers()
        {
            // Arrange
            var type = new TypeModel
            {
                Id = 1,
                BaseName = "TestClass",
                Namespace = "TestNS",
                Type = TypeType.Class,
                StaticVariables = new List<StaticVariableModel>
                {
                    new StaticVariableModel
                    {
                        Name = "s_staticVar",
                        TypeString = "int",
                        Address = "0x12345678",
                        GlobalType = "int TestClass::s_staticVar"
                    }
                }
            };

            var generator = new ClassOutputGenerator(null);

            // Act
            var tokens = generator.Generate(type).ToList();
            var output = string.Concat(tokens.Select(t => t.Text));

            // Assert
            // We expect the generator to output the static member definition
            // Note: int is normalized to int32_t by the generator utilities
            Assert.Contains("static int32_t s_staticVar;", output);
            // And potentially the address comment
            Assert.Contains("// 0x12345678", output);
        }
    }
}
