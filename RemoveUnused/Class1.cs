using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
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
    }
}
