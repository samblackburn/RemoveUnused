using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
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
            var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            var ws = new AdhocWorkspace();
            //Create new project
            var project = ws.AddProject("Sample", "C#");
            project = project.AddMetadataReference(mscorlib);
            //Add project to workspace
            ws.TryApplyChanges(project.Solution);
            var sourceText = SourceText.From(cs);
            //Create new document
            var doc = ws.AddDocument(project.Id, "NewDoc", sourceText);
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
        public void ListCallsInRealSolution()
        {
            var sln = @"E:\repos\SQLCompareEngine\SQLCompare.sln";

            var ws = MakeWorkspace();
            var solution = ws.OpenSolutionAsync(sln).Result;

            Console.WriteLine($"{DateTime.Now} Listing documents...");
            var documents = GetDocuments(solution).ToList();
            Console.WriteLine($"{DateTime.Now} There are {documents.Count} documents.  Generating semantic models...");
            var models = documents.Select(d => d.GetSemanticModelAsync().Result).ToList();
            Console.WriteLine($"{DateTime.Now} There are {models.Count} semantic models.  Generating method invocations...");
            //Get the syntax nodes for all method invocations
            var methodInvocations = documents.SelectMany(d => d.GetSyntaxRootAsync().Result.DescendantNodes().OfType<InvocationExpressionSyntax>()).ToList();
            Console.WriteLine($"{DateTime.Now} There are {methodInvocations.Count} method invocations.  Generating symbols...");
            var methodSymbols = models.SelectMany(m => methodInvocations.SelectMany(i => TryGetSymbol(i, m))).ToList();
            Console.WriteLine($"{DateTime.Now} There are {methodSymbols.Count} symbols.  Generating references to each method...");
            //Finds all references to each method
            var references = methodSymbols.Select(m => new
            {
                Referenced = m.Locations,
                Referencing = documents
                    .SelectMany(d => SymbolFinder.FindReferencesAsync(m, d.Project.Solution).Result)
                    .SelectMany(x => x.Locations)
                    .Select(x => x.Location)
            }).ToList();
            Console.WriteLine($"{DateTime.Now} There are {references.Count} references.  Listing the first 10...");

            foreach (var reference in references.Take(10))
            {
                Console.WriteLine($"    {reference.Referenced.First()} --> {reference.Referencing.First()}");
            }

            /*foreach (var projectId in solution.ProjectIds)
            {
                var project = solution.GetProject(projectId);
                foreach (var documentId in project.DocumentIds)
                {
                    var document = project.GetDocument(documentId);

                    if (document.SourceCodeKind != SourceCodeKind.Regular)
                        continue;

                    var doc = document;
                    doc = Rewrite(doc);

                    project = doc.Project;
                }

                solution = project.Solution;
            }

            ws.TryApplyChanges(solution);*/
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
                result = semanticModel.GetSymbolInfo(invocationExpressionSyntax).Symbol;
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
