using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ACDecompileParser.Shared.Tests.Lib.Storage;

public class TypeRepositoryFunctionBodyTests
{
    private TypeContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TypeContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new TypeContext(options);
    }

    [Fact]
    public void GetFunctionBodiesForMultipleTypes_ShouldIncludeSignatureAndParameters()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new TypeRepository(context);

        var type = new TypeModel
        {
            BaseName = "TestClass",
            Namespace = "",
            StoredFullyQualifiedName = "TestClass",
            Type = TypeType.Class
        };
        context.Types.Add(type);
        context.SaveChanges();

        var signature = new FunctionSignatureModel
        {
            Name = "Method",
            ReturnType = "void",
            CallingConvention = "",
            Parameters = new List<FunctionParamModel>
            {
                new FunctionParamModel { Name = "p1", ParameterType = "int", Position = 0 }
            }
        };
        context.FunctionSignatures.Add(signature);
        context.SaveChanges();

        var body = new FunctionBodyModel
        {
            FullyQualifiedName = "TestClass::Method",
            ParentId = type.Id,
            FunctionSignatureId = signature.Id
        };
        context.FunctionBodies.Add(body);
        context.SaveChanges();

        // Act
        var result = repository.GetFunctionBodiesForMultipleTypes(new[] { type.Id });

        // Assert
        Assert.True(result.ContainsKey(type.Id));
        var bodies = result[type.Id];
        Assert.Single(bodies);
        
        var fetchedBody = bodies.First();
        Assert.NotNull(fetchedBody.FunctionSignature);
        Assert.Equal("Method", fetchedBody.FunctionSignature.Name);
        Assert.NotNull(fetchedBody.FunctionSignature.Parameters);
        Assert.Single(fetchedBody.FunctionSignature.Parameters);
        Assert.Equal("p1", fetchedBody.FunctionSignature.Parameters.First().Name);
    }
}
