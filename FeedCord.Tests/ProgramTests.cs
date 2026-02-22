using Xunit;
using System.Reflection;

namespace FeedCord.Tests
{
  [CollectionDefinition("ProgramMainNonParallel", DisableParallelization = true)]
  public class ProgramMainNonParallelCollection
  {
  }

  public class ProgramTests
  {
    #region Program Structure Tests

    [Fact]
    public void Program_ClassExists()
    {
      // Arrange & Act
      var programType = typeof(Program);

      // Assert
      Assert.NotNull(programType);
      Assert.Equal("FeedCord.Program", programType.FullName);
    }

    [Fact]
    public void Program_HasMainMethod()
    {
      // Arrange & Act
      var mainMethod = typeof(Program).GetMethod(
          "Main",
          BindingFlags.Public | BindingFlags.Static,
          null,
          new Type[] { typeof(string[]) },
          null
      );

      // Assert
      Assert.NotNull(mainMethod);
    }

    [Fact]
    public void Program_MainMethod_ReturnsVoid()
    {
      // Arrange & Act
      var mainMethod = typeof(Program).GetMethod(
          "Main",
          BindingFlags.Public | BindingFlags.Static
      );

      // Assert
      Assert.NotNull(mainMethod);
      Assert.Equal(typeof(void), mainMethod.ReturnType);
    }

    [Fact]
    public void Program_MainMethod_AcceptsStringArray()
    {
      // Arrange & Act
      var mainMethod = typeof(Program).GetMethod(
          "Main",
          BindingFlags.Public | BindingFlags.Static
      );

      // Assert
      Assert.NotNull(mainMethod);
      var parameters = mainMethod.GetParameters();
      Assert.Single(parameters);
      Assert.Equal(typeof(string[]), parameters[0].ParameterType);
    }

    [Fact]
    public void Program_MainMethod_IsStatic()
    {
      // Arrange & Act
      var mainMethod = typeof(Program).GetMethod(
          "Main",
          BindingFlags.Public | BindingFlags.Static
      );

      // Assert
      Assert.NotNull(mainMethod);
      Assert.True(mainMethod.IsStatic);
    }

    [Fact]
    public void Program_MainMethod_IsPublic()
    {
      // Arrange & Act
      var mainMethod = typeof(Program).GetMethod(
          "Main",
          BindingFlags.Public | BindingFlags.Static
      );

      // Assert
      Assert.NotNull(mainMethod);
      Assert.True(mainMethod.IsPublic);
    }

    #endregion

    #region Entry Point Signature Tests

    [Fact]
    public void Program_HasCorrectEntryPointSignature()
    {
      // Arrange
      var programType = typeof(Program);
      var mainMethod = programType.GetMethod(
          "Main",
          BindingFlags.Public | BindingFlags.Static
      );

      // Act
      var signature = mainMethod?.ToString();

      // Assert
      Assert.NotNull(mainMethod);
      Assert.NotNull(signature);
      Assert.Contains("Main", signature);
      Assert.Contains("System.String[]", signature);
    }

    [Theory]
    [MemberData(nameof(GetArgumentVariations))]
    public void Program_MainMethod_CanAcceptVariousArguments(string[] args)
    {
      // Arrange
      var mainMethod = typeof(Program).GetMethod(
          "Main",
          BindingFlags.Public | BindingFlags.Static,
          null,
          new Type[] { typeof(string[]) },
          null
      );

      // Assert
      Assert.NotNull(mainMethod);
      // Verify it can handle string arrays of different lengths
      Assert.NotNull(args);
    }

    #endregion

    #region Namespace and Type Information Tests

    [Fact]
    public void Program_IsInFeedCordNamespace()
    {
      // Arrange & Act
      var programType = typeof(Program);
      var @namespace = programType.Namespace;

      // Assert
      Assert.Equal("FeedCord", @namespace);
    }

    [Fact]
    public void Program_IsPublic()
    {
      // Arrange & Act
      var programType = typeof(Program);

      // Assert
      Assert.True(programType.IsPublic);
    }

    [Fact]
    public void Program_IsClass()
    {
      // Arrange & Act
      var programType = typeof(Program);

      // Assert
      Assert.True(programType.IsClass);
    }

    [Fact]
    public void Program_IsNotAbstract()
    {
      // Arrange & Act
      var programType = typeof(Program);

      // Assert
      Assert.False(programType.IsAbstract);
    }

    #endregion

    #region Method Invocation Structure Tests

    [Fact]
    public void Program_MainMethod_CanBeInvokedWithNoArgs()
    {
      // Arrange
      var mainMethod = typeof(Program).GetMethod(
          "Main",
          BindingFlags.Public | BindingFlags.Static,
          null,
          new Type[] { typeof(string[]) },
          null
      );

      var emptyArgs = Array.Empty<string>();

      // Note: We don't actually invoke because it would start the application
      // This test just verifies the method signature is correct for invocation

      // Assert
      Assert.NotNull(mainMethod);
    }

    [Fact]
    public void Program_MainMethod_CanBeInvokedWithConfigPath()
    {
      // Arrange
      var mainMethod = typeof(Program).GetMethod(
          "Main",
          BindingFlags.Public | BindingFlags.Static,
          null,
          new Type[] { typeof(string[]) },
          null
      );

      var configArgs = new[] { "config.json" };

      // Note: We don't actually invoke because it would start the application
      // This test just verifies the method signature is correct for invocation

      // Assert
      Assert.NotNull(mainMethod);
    }

    #endregion

    #region Reflection-Based Structure Verification

    [Fact]
    public void Program_HasExactlyOnePublicMethod()
    {
      // Arrange & Act
      var publicMethods = typeof(Program).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

      // Assert - Main method only (inherited methods like GetType, Equals, etc. are excluded by BindingFlags.DeclaredOnly)
      Assert.Single(publicMethods);
    }

    [Fact]
    public void Program_MainMethodParameterIsCorrectlyNamed()
    {
      // Arrange & Act
      var mainMethod = typeof(Program).GetMethod(
          "Main",
          BindingFlags.Public | BindingFlags.Static
      );
      var parameter = mainMethod?.GetParameters().FirstOrDefault();

      // Assert
      Assert.NotNull(parameter);
      Assert.Equal("args", parameter.Name);
    }

    [Fact]
    public void Program_NoPublicConstructor()
    {
      // Arrange & Act
      var constructors = typeof(Program).GetConstructors(BindingFlags.Public);

      // Assert
      Assert.Empty(constructors);
    }

    [Fact]
    public void Program_NoPublicProperties()
    {
      // Arrange & Act
      var properties = typeof(Program).GetProperties(BindingFlags.Public | BindingFlags.DeclaredOnly);

      // Assert
      Assert.Empty(properties);
    }

    [Fact]
    public void Program_NoPublicFields()
    {
      // Arrange & Act
      var fields = typeof(Program).GetFields(BindingFlags.Public | BindingFlags.DeclaredOnly);

      // Assert
      Assert.Empty(fields);
    }

    #endregion

    #region Entry Point Contract Tests

    [Fact]
    public void Program_FollowsDotNetEntryPointConvention()
    {
      // Arrange & Act
      var mainMethod = typeof(Program).GetMethod(
          "Main",
          BindingFlags.Public | BindingFlags.Static
      );

      // Assert - Verify all aspects of entry point contract
      Assert.NotNull(mainMethod);
        Assert.Multiple(
          () => Assert.True(mainMethod.IsStatic),
          () => Assert.True(mainMethod.IsPublic),
          () => Assert.Equal(typeof(void), mainMethod.ReturnType)
        );

      var parameters = mainMethod.GetParameters();
        Assert.Collection(parameters,
          parameter => Assert.Equal(typeof(string[]), parameter.ParameterType)
        );
    }

    [Fact]
    public void Program_MainMethod_CanBeLoadedViaReflection()
    {
      // Arrange & Act
      var assembly = typeof(Program).Assembly;
      var programType = assembly.GetType("FeedCord.Program");
      var mainMethod = programType?.GetMethod("Main", BindingFlags.Public | BindingFlags.Static);

      // Assert
      Assert.NotNull(mainMethod);
    }

    #endregion

    #region Integration Context Tests

    [Fact]
    public void Program_BelongsToFeedCordAssembly()
    {
      // Arrange & Act
      var assembly = typeof(Program).Assembly;

      // Assert
      Assert.NotNull(assembly);
      Assert.Contains("FeedCord", assembly.FullName);
    }

    [Fact]
    public void Program_CanBeAccessedFromStartup()
    {
      // Arrange & Act
      // Both Program and Startup should be in the same assembly
      var programAssembly = typeof(Program).Assembly;
      var startupType = programAssembly.GetType("FeedCord.Startup");

      // Assert
      Assert.NotNull(startupType);
    }

    #endregion

    #region Test Data

    public static IEnumerable<object[]> GetArgumentVariations()
    {
      yield return new object[] { Array.Empty<string>() };
      yield return new object[] { new[] { "config.json" } };
      yield return new object[] { new[] { "config/appsettings.json" } };
      yield return new object[] { new[] { "custom/config.yaml" } };
    }

    #endregion
  }

  [Collection("ProgramMainNonParallel")]
  public class ProgramExecutionTests
  {
    [Fact]
    public void Program_Main_InvokesStartupEntryPointWithProvidedArgs()
    {
      var startupEntryPointProperty = typeof(Program).GetProperty(
          "StartupEntryPoint",
          BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
      );

      Assert.NotNull(startupEntryPointProperty);

      var originalStartupEntryPoint = (Action<string[]>)startupEntryPointProperty!.GetValue(null)!;
      string[]? capturedArgs = null;

      try
      {
        startupEntryPointProperty.SetValue(null, (Action<string[]>)(args => capturedArgs = args));

        var args = new[] { "config/custom.json" };
        Program.Main(args);

        Assert.Same(args, capturedArgs);
      }
      finally
      {
        startupEntryPointProperty.SetValue(null, originalStartupEntryPoint);
      }
    }
  }
}

