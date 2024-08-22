using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UdonSharpInline {
    public class InvocationReplacer : CSharpSyntaxRewriter
    {
        private readonly InvocationExpressionSyntax _targetInvocation;
        private readonly IEnumerable<StatementSyntax> _replacementStatements;

        public InvocationReplacer(InvocationExpressionSyntax targetInvocation, IEnumerable<StatementSyntax> replacementStatements)
        {
            _targetInvocation = targetInvocation;
            _replacementStatements = replacementStatements;
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node) {
            return node.IsEquivalentTo(_targetInvocation) ?
                SyntaxFactory.Block(_replacementStatements) : base.VisitInvocationExpression(node);
        }
        
    }
}