using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using Moq;
using Xunit;
using ACDecompileParser.Shared.Lib.Storage;

namespace ACDecompileParser.Tests.Lib.Output
{
    public class ReproductionTest
    {
        [Fact]
        public void Generate_ClassWithFunctionBodies_OutputsSignatures()
        {
            // Arrange
            var type = new TypeModel
            {
                Id = 1,
                BaseName = "TestClass",
                Namespace = "TestNS",
                Type = TypeType.Class,
                FunctionBodies = new List<FunctionBodyModel>
                {
                    new FunctionBodyModel
                    {
                        FullyQualifiedName = "TestNS::TestClass::MyFunction",
                        FunctionSignature = new FunctionSignatureModel
                        {
                            Name = "MyFunction",
                            ReturnType = "void",
                            Parameters = new List<FunctionParamModel>()
                        }
                    }
                }
            };

            var generator = new ClassOutputGenerator(null);

            // Act
            var tokens = generator.Generate(type).ToList();
            var output = string.Concat(tokens.Select(t => t.Text));

            // Assert
            Assert.Contains("void MyFunction();", output);
        }
    }
}
