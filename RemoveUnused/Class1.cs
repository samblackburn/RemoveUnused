using System;
using System.Linq;
using System.Threading.Tasks;
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
            var tree = CSharpSyntaxTree.ParseText(@"
    public class MyClass
    {
        public void MyMethod()
        {
        }
    }");

            var syntaxRoot = tree.GetRoot();
            var MyClass = syntaxRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            var MyMethod = syntaxRoot.DescendantNodes().OfType<MethodDeclarationSyntax>().First();

            Console.WriteLine(MyClass.Identifier.ToString());
            Console.WriteLine(MyMethod.Identifier.ToString());
        }
    }
}
