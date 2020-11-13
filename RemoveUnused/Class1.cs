using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace RemoveUnused
{
    [TestFixture]
    public class Class1
    {
        [Test]
        public void Foo()
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
        public async Task RealSolution()
        {
            var sln = @"E:\repos\SQLCompareEngine\SQLCompare.sln";

            MSBuildWorkspace ws;
            try
            {
                ws = MSBuildWorkspace.Create();
            }
            catch (ReflectionTypeLoadException ex)
            {
                throw ex.LoaderExceptions.First();
            }

            var solution = await ws.OpenSolutionAsync(sln);
            foreach (var project in solution.Projects)
            {
                foreach (var document in project.Documents)
                {
                    Console.WriteLine(project.Name + "\t\t\t" + document.Name);
                }
            }
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
