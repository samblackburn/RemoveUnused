using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace RemoveUnused
{
    [TestFixture]
    public class Class1
    {
        /// <summary>
        /// Prints "SourceFile(NewDoc[68..76)) --> SourceFile(NewDoc[44..52))"
        /// </summary>
        /// <details>
        /// This test successfully identifies the relationship between a method call and the method being called.
        /// </details>
        [Test]
        public void OneProject()
        {
            var cs = @"public class MyClass{public void Consumer(){Consumed();}public void Consumed(){}}";
            //                                          33 -- 41   44 -- 52                68 -- 76
            var doc = MakeDocumentFromString(cs);
            //Get the semantic model
            var model = doc.GetSemanticModelAsync().Result;
            //Get the syntax nodes for all method invocations
            var methodInvocations = doc.GetSyntaxRootAsync().Result.DescendantNodes().OfType<InvocationExpressionSyntax>();
            var methodSymbols = methodInvocations.Select(x => model.GetSymbolInfo(x).Symbol);
            //Finds all references to each method
            var references = methodSymbols.Select(async m => new
            {
                Referenced = m.Locations,
                Referencing = (await SymbolFinder.FindReferencesAsync(m, doc.Project.Solution)).SelectMany(x => x.Locations).Select(x => x.Location)
            });

            foreach (var reference in references.Select(x => x.Result))
            {
                Console.WriteLine($"{reference.Referenced.First()} --> {reference.Referencing.First()}");
            }
        }

        /// <summary>
        /// Prints "SourceFile(Referenced[41..49)) --> SourceFile(Referencing[54..62))"
        /// </summary>
        /// <details>
        /// This test successfully identifies the relationship between a method call and the method being called, across a csproj boundary.
        /// </details>
        [Test]
        public async Task TwoProjects()
        {
            const string referencing = @"public class Consumer{public void Consumer(){Consumed.Consumed();}}";
            const string referenced = @"public class Consumed{public static void Consumed(){}}";

            var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            var ws = new AdhocWorkspace();
            var project1 = ws.AddProject("Referenced", "C#").AddMetadataReference(mscorlib);
            var project2 = ws.AddProject("Referencing", "C#").AddMetadataReference(mscorlib);
            ws.TryApplyChanges(project1.Solution);
            ws.TryApplyChanges(project2.Solution);
            var documents = new[]
            {
                ws.AddDocument(project1.Id, "Referenced", SourceText.From(referenced)),
                ws.AddDocument(project1.Id, "Referencing", SourceText.From(referencing))
            };

            var models = documents.Select(d => d.GetSemanticModelAsync().Result);
            //Get the syntax nodes for all method invocations
            var methodInvocations = documents.SelectMany(d => d.GetSyntaxRootAsync().Result.DescendantNodes().OfType<InvocationExpressionSyntax>());
            var methodSymbols = models.SelectMany(m => methodInvocations.SelectMany(i => TryGetSymbol(i, m)));
            //Finds all references to each method
            var references = methodSymbols.Select(m => new
            {
                Referenced = m.Locations,
                Referencing = documents
                    .SelectMany(d => SymbolFinder.FindReferencesAsync(m, d.Project.Solution).Result)
                    .SelectMany(x => x.Locations)
                    .Select(x => x.Location)
            });

            foreach (var reference in references)
            {
                Console.WriteLine($"{reference.Referenced.First()} --> {reference.Referencing.First()}");
            }
        }

        [Test]
        public async Task ListDocumentsInRealSolution()
        {
            var sln = @"E:\repos\SQLCompareEngine\SQLCompare.sln";

            var ws = MakeWorkspace();

            var solution = await ws.OpenSolutionAsync(sln);
            foreach (var document in GetDocuments(solution))
            {
                Console.WriteLine(document.Name);
            }
        }

        private static IEnumerable<Document> GetDocuments(Solution solution)
        {
            foreach (var projectId in solution.ProjectIds)
            {
                var project = solution.GetProject(projectId);
                foreach (var documentId in project.DocumentIds)
                {
                    var document = project.GetDocument(documentId);

                    if (document.SourceCodeKind != SourceCodeKind.Regular)
                        continue;

                    project = document.Project;
                    yield return document;
                }

                solution = project.Solution;
            }
        }

        public static void Main()
        {
            
        }

        [Test]
        public async Task ListCallsInRealSolution()
        {
            var sln = @"E:\repos\SQLCompareEngine\SQLCompare.sln";

            var ws = MakeWorkspace();
            var solution = await ws.OpenSolutionAsync(sln);

            Console.WriteLine($"{DateTime.Now} Listing documents...");
            var documents = GetDocuments(solution).ToList();

            Console.WriteLine($"{DateTime.Now} There are {documents.Count} documents.  Generating method invocations...");
            //Get the syntax nodes for all method invocations
            var methodInvocations = documents.SelectMany(doc => doc
                    .GetSyntaxRootAsync().Result
                    .DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Select(invocation => new
                    {
                        invocation,
                        doc.GetSemanticModelAsync().Result.GetSymbolInfo(invocation).Symbol
                    }))
                .Where(x => x != null).ToList();

            Console.WriteLine($"{DateTime.Now} There are {methodInvocations.Count} invocations.  Listing the first 10...");
            foreach (var reference in methodInvocations.Take(10))
            {
                Console.WriteLine($"    {reference.invocation.Parent} --> {reference.Symbol?.ContainingSymbol.Name}.{reference.Symbol?.Name}");
            }
        }

        [Test]
        public async Task ListUnusedMethodsRealSolution()
        {
            var sln = @"E:\repos\SQLCompareEngine\SQLCompare.sln";

            var ws = MakeWorkspace();
            var solution = await ws.OpenSolutionAsync(sln);
            Console.WriteLine($"{DateTime.Now} Hi.");

            var documents = GetDocuments(solution)
                .Where(d => d.Name.Contains("CustomDeploymentScriptStatementTests") ||
                                    d.Name.Contains("DeploymentScriptStatementFactory"))
                .ToList();
            Console.WriteLine($"{DateTime.Now} There are {documents.Count} documents.");
            foreach (var document in documents)
            {
                Console.WriteLine($"    {document.Name}");
            }

            var allMethods = documents.Where(d => d.Project.Name == "SQLCompareEngine(net472)").SelectMany(d => d
                .GetSyntaxRootAsync().Result
                .DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Select(method => d.GetSemanticModelAsync().Result.GetDeclaredSymbol(method))
                ).ToList();
            Console.WriteLine($"{DateTime.Now} There are {allMethods.Count} methods:");
            foreach (var method in allMethods)
            {
                Console.WriteLine($"    {method.Name}");
            }

            var methodsUsed = documents.SelectMany(doc => doc
                    .GetSyntaxRootAsync().Result
                    .DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Select(invocation => new {Consumer = invocation, Consumed = doc.GetSemanticModelAsync().Result.GetSymbolInfo(invocation).Symbol}))
                /*.Where(x => x != null)*/.ToList();
            Console.WriteLine($"{DateTime.Now} There are {methodsUsed.Count} invocations.");
            foreach (var method in methodsUsed)
            {
                Console.WriteLine($"    {RemoveWhitespace(method.Consumer)} => {method.Consumed?.ContainingType.Name}.{method.Consumed?.Name}");
            }

            var unusedMethods = allMethods.Except(methodsUsed.Select(m => m.Consumed), SymbolEqualityComparer.Default).ToList();
            Console.WriteLine($"{DateTime.Now} There are {unusedMethods.Count} unused methods. First 10:");
            foreach (var unused in unusedMethods.Take(10))
            {
                Console.WriteLine($"    {unused.ContainingType.Name}.{unused.Name}");
            }
        }

        [Test]
        public void RemoveArbitraryMethod()
        {
            var cs = @"public class MyClass
{
    /// <summary>sdfg</summary>
    public void Consumer ()
    {
         Consumed();
    }
    /// <blah>
    public void Consumed() {}
}";
            var document = MakeDocumentFromString(cs);
            var root = document.GetSyntaxRootAsync().Result;
            var nodes = root.DescendantNodes();

            // In the real implementation, the start/end will come from the R# report XML
            var methodToRemove = "Consumer";
            var start = cs.IndexOf(methodToRemove);
            var end = start + methodToRemove.Length;
            Assert.Positive(start);

            var found = nodes.Last(n => n.Span.Start < start && n.Span.End > end);
            Assert.That(found, Is.InstanceOf<MethodDeclarationSyntax>());
            Console.WriteLine($"Removing {found.Span.Start}-{found.Span.End} {found}");
            Console.WriteLine("-------------------");

            root = root.ReplaceNode(found, new List<SyntaxNode>());
            Console.WriteLine("Modified doc:");
            Console.WriteLine(root.ToString());
        }

        private static Document MakeDocumentFromString(string cs)
        {
            var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            var ws = new AdhocWorkspace();
            //Create new project
            var project = ws.AddProject("Sample", "C#");
            project = project.AddMetadataReference(mscorlib);
            //Add project to workspace
            ws.TryApplyChanges(project.Solution);
            var sourceText = SourceText.From(cs);
            //Create new document
            return ws.AddDocument(project.Id, "NewDoc", sourceText);
        }

        private string RemoveWhitespace(InvocationExpressionSyntax blah)
        {
            return blah.ToString().Replace(" ", "").Replace("\r", "").Replace("\n", "");
        }

        private static MSBuildWorkspace MakeWorkspace()
        {
            MSBuildWorkspace ws;
            try
            {
                ws = MSBuildWorkspace.Create();
            }
            catch (ReflectionTypeLoadException ex)
            {
                Console.WriteLine(ex.StackTrace);
                throw ex.LoaderExceptions.First();
            }

            return ws;
        }

        private Document Rewrite(Document doc)
        {
            return doc;
        }

        private static IEnumerable<ISymbol> TryGetSymbol(
            ExpressionSyntax invocationExpressionSyntax,
            SemanticModel semanticModel)
        {
            ISymbol result;
            try
            {
                result = ModelExtensions.GetSymbolInfo(semanticModel, invocationExpressionSyntax).Symbol;
            }
            catch
            {
                yield break;
            }

            if (result != null)
            {
                yield return result;
            }
        }
    }
}
