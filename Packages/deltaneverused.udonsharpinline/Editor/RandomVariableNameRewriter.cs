using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UdonSharpInline {
    public class RandomVariableNameRewriter : CSharpSyntaxRewriter {
        private readonly Dictionary<string, string> _nameMappings;

        public RandomVariableNameRewriter(Dictionary<string, string> nameMappings) {
            _nameMappings = nameMappings;
        }

        public override SyntaxNode VisitVariableDeclarator(VariableDeclaratorSyntax node) {
            if (_nameMappings.TryGetValue(node.Identifier.ValueText, out var newName)) {
                var newIdentifier = SyntaxFactory.Identifier(newName);
                var newDeclarator = node.WithIdentifier(newIdentifier);
                return base.VisitVariableDeclarator(newDeclarator);
            }

            return base.VisitVariableDeclarator(node);
        }
        
        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (_nameMappings.TryGetValue(node.Identifier.ValueText, out var newName))
            {
                var newIdentifier = SyntaxFactory.IdentifierName(newName);
                return base.VisitIdentifierName(newIdentifier);
            }

            return base.VisitIdentifierName(node);
        }
    }
}