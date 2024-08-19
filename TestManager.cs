using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using NUnit;
using NUnit.Engine;
using System.Reflection;
using System.Text.Json;
using System.Xml;

namespace LittleRosie;

public class TestManager 
{ 
    private string assemblyName = "main.dll";

    public IEnumerable<TestResult> Run() 
    {
        using var engine = TestEngineActivator.CreateInstance();
        engine.InternalTraceLevel = InternalTraceLevel.Off;
        var assembly = Assembly.LoadFrom(assemblyName);
        var package = new TestPackage(assemblyName);

        using var runner = engine.GetRunner(package);
        XmlNode testResult = runner.Run(null, TestFilter.Empty);
        return parseTestResult(testResult);
    }

    public BuildResult Build(string lines) 
    {
        try 
        {
            if (string.IsNullOrEmpty(lines)) 
            { 
                return new BuildResult 
                {
                    Status = StatusType.INTERNAL_ERROR,
                    Message = "empty input",
                };
            }

            var submissions = JsonSerializer.Deserialize<Submission>(lines);
            if (submissions == null)
            {
                return new BuildResult 
                {
                    Status = StatusType.INTERNAL_ERROR,
                    Message = "serialization failed",
                };
            }

            // implicit using workaround
            // https://github.com/dotnet/roslyn/issues/58119
            var implicingUsings = CSharpSyntaxTree.ParseText(SourceText.From("""
            global using global::System;
            global using global::System.IO;
            global using global::System.Linq;
            global using global::System.Net.Http;
            global using global::System.Threading;
            global using global::System.Threading.Tasks;
            global using global::System.Collections.Generic;
            """))
            .WithFilePath("GlobalUsings.cs");

            var syntaxTrees = submissions.SourceFiles.Where(el => !string.IsNullOrEmpty(el.SourceCode))
                            .Select(el => { 
                                // remove main method for non-test source codes
                                var source = SourceText.From(el.SourceCode);
                                var tree = CSharpSyntaxTree.ParseText(source).WithFilePath(el.Filename);
                                var root = tree.GetRoot();
                                var main = root.DescendantNodes()
                                               .OfType<MethodDeclarationSyntax>()
                                               .FirstOrDefault(m => m.Identifier.Text == "Main");

                                if (main == null)
                                {
                                    return tree;
                                }

                                var newRoot = root.RemoveNode(main, SyntaxRemoveOptions.KeepExteriorTrivia);
                                var classNode = main.Parent as ClassDeclarationSyntax;
                                if (classNode != null && !classNode.Members.Any())
                                {
                                    // remove the class if it has no members
                                    newRoot = newRoot!.RemoveNode(classNode, SyntaxRemoveOptions.KeepNoTrivia);
                                }

                                return SyntaxFactory.SyntaxTree(newRoot!);
                            }).Prepend(implicingUsings).ToList();

            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                            .Split(Path.PathSeparator)
                            .Where(el => !string.IsNullOrEmpty(el))
                            .Select(el => MetadataReference.CreateFromFile(el));

            if (references.Count() == 0) 
            {
                return new BuildResult 
                {
                    Status = StatusType.INTERNAL_ERROR,
                    Message = "no trusted assemblies",
                };
            }

            var compilationOptions = new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                                                            .WithOptimizationLevel(OptimizationLevel.Release)
                                                            .WithPlatform(Platform.X64)
                                                            .WithWarningLevel(1);

            var compilation = CSharpCompilation.Create(assemblyName)
                                               .WithOptions(compilationOptions)
                                               .AddReferences(references)
                                               .AddSyntaxTrees(syntaxTrees);

            // FIXME: suppression does not work for whatever reason, so here is a workaround
            var errors = compilation.GetDiagnostics().Where(el => el.Id != "CS5001" && el.Severity == DiagnosticSeverity.Error);
            if (errors.Count() > 0)
            {
                return new BuildResult 
                { 
                    Status = StatusType.ERROR, 
                    CompilatioErrors = Diagnostics(errors),
                };
            }

            // there should be only one test file
            if (string.IsNullOrEmpty(submissions.SourceCodeTest)) 
            {
                return new BuildResult 
                { 
                    Status = StatusType.ERROR, 
                    Message = "no test provided",
                };
            }

            var testTree = CSharpSyntaxTree.ParseText(SourceText.From(submissions.SourceCodeTest))
                                           .WithFilePath(Path.GetRandomFileName() + ".cs");
            var completeTree = syntaxTrees.Append(testTree);

            compilation = compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(completeTree);

            var emitResult = compilation.Emit(assemblyName);
            if (emitResult.Success && errors.Count() == 0)
            {
                return new BuildResult { Status = StatusType.OK };
            }
            else
            {
                return new BuildResult 
                { 
                    Status = StatusType.ERROR, 
                    CompilatioErrors = Diagnostics(emitResult.Diagnostics.Where(el => el.Severity == DiagnosticSeverity.Error)),
                };
            }
        } 
        catch(Exception e) 
        {
            return new BuildResult 
            { 
                Status = StatusType.INTERNAL_ERROR,
                Message = e.Message,
            };
        }
    }

    public CompileError[] Diagnostics(IEnumerable<Diagnostic> errors)
    {
        var compilationError = new List<CompileError>();
        foreach(var d in errors)
        {
            var location = d.Location;
            var line = location.GetLineSpan().StartLinePosition;
            var compileError = new CompileError 
            {
                Filename = location.SourceTree?.FilePath ?? "",
                Message = d.GetMessage(),
                Line = line.Line,
                Character = line.Character,
            };
            compilationError.Add(compileError);
        }

        return compilationError.ToArray();
    }

    private IEnumerable<TestResult> parseTestResult(XmlNode node)
    {
        var testResults = new List<TestResult>();
        var testCaseNodes = node.SelectNodes("//test-case");
        if (testCaseNodes == null)
        {
            return testResults;
        }

        foreach (XmlNode testCase in testCaseNodes)
        {
            string status = testCase.GetAttribute("result").ToUpper();

            string message;
            if (status == "PASSED")
            {
                message = testCase?.SelectSingleNode("reason/message")?.InnerText ?? "";
            }
            else
            {
                message = testCase?.SelectSingleNode("reason/failure")?.InnerText ?? "";
            }

            var result = new TestResult
            {
                Status = testCase.GetAttribute("result").ToUpper(),
                Name = testCase.GetAttribute("name"),
                Output = message,
            };

            testResults.Add(result);
        }

        return testResults;
    }
}
