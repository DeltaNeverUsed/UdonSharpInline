using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEngine;

namespace UdonSharpInline {
    public class FunctionInserterRewriter : CSharpSyntaxRewriter {
        private readonly InvocationExpressionSyntax _target;
        private readonly BlockSyntax _body;
        private BlockSyntax _newBody;
        
        private static System.Random _random = new();
        
        private static string GenerateRandomName() {
            const string chars = "abcdefghijklmnopqrstuvwxyz";
            return new string(Enumerable.Repeat(chars, 5)
                .Select(s => s[_random.Next(0, chars.Length - 1)])
                .ToArray());
        }

        private void RenameVars() {
            var existingVariableNames = _body.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Select(v => v.Identifier.ValueText)
                .Distinct()
                .ToList();

            var newVariableNames = existingVariableNames.ToDictionary(
                oldName => oldName,
                oldName => $"{GenerateRandomName()}__{oldName}"
            );
            // Rename function vars to be unique
            var rewriter = new RandomVariableNameRewriter(newVariableNames);
            _newBody = ((BlockSyntax)rewriter.Visit(_body));
        }

        public FunctionInserterRewriter(InvocationExpressionSyntax target, BlockSyntax body) {
            _target = target;
            _body = body;
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            // Create a new statement to insert above the invocation
            var newStatement = SyntaxFactory.ParseStatement("Console.WriteLine(\"Inserted above\");\n");

            // Get the parent statement of the invocation (usually an ExpressionStatementSyntax)
            var parentStatement = node.Ancestors().OfType<StatementSyntax>().First();

            // Get the parent block that contains the statement
            var parentBlock = parentStatement.Parent as BlockSyntax;

            // Insert the new statement before the parent statement within the block
            var newStatements = parentBlock.Statements.Insert(parentBlock.Statements.IndexOf(parentStatement), newStatement);

            // Replace the old block with the new block in the syntax tree
            var newBlock = parentBlock.WithStatements(newStatements);

            // Replace the parent block with the updated block
            return newBlock;
        }

        public override SyntaxNode Visit(SyntaxNode node) {
            return base.Visit(node);
        }
    }
}