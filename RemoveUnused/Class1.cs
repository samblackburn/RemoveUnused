using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RemoveUnused
{
    [TestFixture]
    public class Class1
    {
        [Test]
        public async Task Foo()
        {
            var tree = CSharpSyntaxTree.ParseText(
@"public class MyClass{public void Consumer(){Consumed();}public void Consumed(){}}");
            var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            var compilation = CSharpCompilation.Create("MyCompilation",
                syntaxTrees: new[] { tree }, references: new[] { mscorlib });
            //Note that we must specify the tree for which we want the model.
            //Each tree has its own semantic model
            var model = compilation.GetSemanticModel(tree);

            var descendantNodes = (await tree.GetRootAsync()).DescendantNodes().ToArray();
            foreach (var node in descendantNodes)
            {
                Console.WriteLine($"{(node.GetText() + "            ").Substring(0,10)}\t{node.GetType().Name}");
            }

            Console.WriteLine();

            var unusedMethods = descendantNodes.OfType<MethodDeclarationSyntax>().ToList();

            foreach (var method in unusedMethods)
            {
                Console.WriteLine(model.GetSymbolInfo(method).Symbol);
            }

            var calls = descendantNodes.OfType<InvocationExpressionSyntax>();

            foreach (var call in calls)
            {
                var callee = model.GetSymbolInfo(call).Symbol;
                Console.WriteLine($"{call}: {callee}");
            }
        }
    }
}
