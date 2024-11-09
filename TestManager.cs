using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Xunit.Runners;

namespace LittleRosie;

public class TestManager 
{ 
    private string assemblyName = "main.dll";

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
            SourceFile[] sources = [.. submissions.SourceCodeTest, .. submissions.Sources];

            var syntaxTrees = sources.Where(el => !string.IsNullOrEmpty(el.SourceCode))
                            .Select(el => { 
                                // remove main method from source codes
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

            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                                                            .WithOptimizationLevel(OptimizationLevel.Release)
                                                            .WithPlatform(Platform.X64)
                                                            .WithWarningLevel(1);

            var compilation = CSharpCompilation.Create(assemblyName)
                                               .WithOptions(compilationOptions)
                                               .AddReferences(references)
                                               .AddSyntaxTrees(syntaxTrees);

            var errors = compilation.GetDiagnostics().Where(el => el.Severity == DiagnosticSeverity.Error);
            if (errors.Any())
            {
                return new BuildResult 
                { 
                    Status = StatusType.ERROR, 
                    CompilatioErrors = Diagnostics(errors),
                };
            }

            var emitResult = compilation.Emit(assemblyName);
            if (emitResult.Success)
            {
                return new BuildResult { Status = StatusType.OK };
            }

            return new BuildResult 
            { 
                Status = StatusType.ERROR, 
                CompilatioErrors = Diagnostics(emitResult.Diagnostics.Where(el => el.Severity == DiagnosticSeverity.Error)),
            };
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
                Line = line.Line + 1,
                Character = line.Character + 1,
            };
            compilationError.Add(compileError);
        }

        return compilationError.ToArray();
    }

    public IEnumerable<TestResult> Tests(string assemblyPath)
    {
        // a bit hacky?
        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            AssemblyName assemblyName = new AssemblyName(args.Name);
            string assemblyPath = Path.Combine(AppContext.BaseDirectory, assemblyName.Name + ".dll");
            if (File.Exists(assemblyPath))
            {
                return Assembly.LoadFrom(assemblyPath);
            }

            return Assembly.Load(args.Name);
        };

        var stack = new ConcurrentStack<TestResult>();

        using var completionEvent = new ManualResetEventSlim(false);
        using var runner = AssemblyRunner.WithoutAppDomain(assemblyPath);
        runner.OnTestFailed = info =>
        {
            stack.Push(new TestResult {
                Passed = false,
                Name = info.TestDisplayName,
                StackTrace = info.ExceptionStackTrace,
            });
        };

        runner.OnTestPassed = info =>
        {
            stack.Push(new TestResult {
                Passed = true,
                Name = info.TestDisplayName,
            });
        };

        runner.OnExecutionComplete = _ =>  
        {
            completionEvent.Set();
        };

        runner.Start();

        completionEvent.Wait();

        return stack.ToArray();
    }
}
