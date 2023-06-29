using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NotifyGenerator
{
    using static SyntaxFactory;
    class Program
    {
        private static SyntaxTrivia space = Whitespace(" ");

        private static string FieldName(SyntaxToken token)
        {
            return "_" + string.Concat(char.ToLowerInvariant(token.Text[0]), token.Text.Substring(1));
        }

        private static SyntaxToken FieldNameToken(SyntaxToken token)
        {
            return Identifier(FieldName(token));
        }

        private static IEnumerable<FieldDeclarationSyntax> CreateField(VariableDeclarationSyntax variable)
        {
            foreach (var declerator in variable.Variables)
            {
                yield return FieldDeclaration(VariableDeclaration(variable.Type.WithoutTrivia().WithTrailingTrivia(space))
                        .WithVariables(SingletonSeparatedList(VariableDeclarator(FieldNameToken(declerator.Identifier)))))
                    .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword).WithTrailingTrivia(space)));
            }
        }

        private static IEnumerable<PropertyDeclarationSyntax> CreateProperty(VariableDeclarationSyntax variable)
        {
            foreach (var declerator in variable.Variables)
            {
                yield return PropertyDeclaration(variable.Type.WithoutTrivia().WithTrailingTrivia(space), declerator.Identifier)
                    .WithTrailingTrivia(CarriageReturnLineFeed)
                    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword).WithTrailingTrivia(space)))
                    .AddAccessorListAccessors(
                        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                            .WithLeadingTrivia(CarriageReturnLineFeed, Tab)
                            .WithBody(Block(ReturnStatement(IdentifierName(FieldNameToken(declerator.Identifier)).WithLeadingTrivia(space)).WithLeadingTrivia(space).WithTrailingTrivia(space))),
                        AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                            .WithLeadingTrivia(CarriageReturnLineFeed, Tab)
                            .WithBody(Block(ExpressionStatement(InvocationExpression(IdentifierName("SetValue"), ArgumentList(SeparatedList(new ArgumentSyntax[] { Argument(IdentifierName(FieldName(declerator.Identifier))).WithRefKindKeyword(Token(SyntaxKind.RefKeyword).WithTrailingTrivia(space)), Argument(IdentifierName("value")).WithLeadingTrivia(space) }))))).WithLeadingTrivia(space).WithTrailingTrivia(space))
                        .WithTrailingTrivia(CarriageReturnLineFeed));
            }
        }

        private static void ProcessClass(ClassDeclarationSyntax @class)
        {
            var variables = @class.DescendantNodes().OfType<VariableDeclarationSyntax>().ToArray();
            foreach (var variable in variables)
                Console.WriteLine(string.Join(Environment.NewLine, CreateField(variable)));


            Console.WriteLine();

            foreach (var variable in variables)
                Console.WriteLine(string.Join(Environment.NewLine, CreateProperty(variable)));
        }

        private static string ReadPostData()
        {
            var contentLength = Convert.ToInt32(Environment.GetEnvironmentVariable("CONTENT_LENGTH"));
            var buffer = new char[contentLength];
            Console.In.Read(buffer, 0, contentLength);
            return new string(buffer);
        }

        static void Main(string[] args)
        {
            SyntaxTree tree = null;
            if (args.Contains("-http"))
            {
                if (Environment.GetEnvironmentVariable("REQUEST_METHOD") != "POST")
                {
                    Console.WriteLine("Not a Post");
                    return;
                }

                tree = CSharpSyntaxTree.ParseText(ReadPostData());
            }
            else if (args.Any())
                tree = CSharpSyntaxTree.ParseText(File.ReadAllText(args[0]));
            else
                tree = CSharpSyntaxTree.ParseText(Console.In.ReadToEnd());

            var root = tree.GetRoot();
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var @class in classes)
                ProcessClass(@class);

            Console.ReadKey();
        }
    }
}
