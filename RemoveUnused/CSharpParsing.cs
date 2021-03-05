using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace RemoveUnused
{
    [TestFixture]
    public class CSharpParsing
    {
        public static void Main() { }

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
            // In the real implementation, the start/end will come from the R# report XML
            var methodToRemove = "Consumer";
            var start = cs.IndexOf(methodToRemove);
            var end = start + methodToRemove.Length;
            Assert.Positive(start);
            var issue = new Issue("", "", start, end, 4, "");

            var modified = RemoveMethodWhoseNameIsAtPosition(cs, issue);
            Console.WriteLine("Modified doc:");
            Console.WriteLine(modified);
        }

        private static string RemoveMethodWhoseNameIsAtPosition(string cs, Issue issue)
        {
            var document = MakeDocumentFromString(cs);
            var root = document.GetSyntaxRootAsync().Result;
            var nodes = root.DescendantNodes();


            var found = nodes.Last(n => n.Span.Start < issue.Start && n.Span.End > issue.End);
            Assert.That(found, Is.InstanceOf<MethodDeclarationSyntax>());
            Console.WriteLine($"Removing {found.Span.Start}-{found.Span.End} {found}");
            Console.WriteLine("-------------------");

            root = root.ReplaceNode(found, new List<SyntaxNode>());
            return root.ToString();
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
    }
}
