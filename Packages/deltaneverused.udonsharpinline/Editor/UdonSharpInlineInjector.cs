using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEngine;

namespace UdonSharpInline {
    public class UdonSharpInlineInjector : CSharpSyntaxRewriter {
        private bool _isRoot = true;
        private CompilationUnitSyntax _root;

        private List<MethodDeclarationSyntax> _inlineAbleMethods;

        public MethodDeclarationSyntax InlineMethodCalls(MethodDeclarationSyntax method) {
            if (method.Body == null)
                return method;

            var invocations = method.Body.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(inv =>
                    _inlineAbleMethods.Any(
                        m => inv.ArgumentList.Arguments.Count == m.ParameterList.Parameters.Count));

            var invocationMethodPairs = new Dictionary<InvocationExpressionSyntax, MethodDeclarationSyntax>();
            foreach (var invocation in invocations) {
                var invocationName = invocation.Expression is IdentifierNameSyntax identifierName
                    ? identifierName.Identifier.Text
                    : (invocation.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.Text;
                var methodDeclaration = _inlineAbleMethods.FirstOrDefault(m => m.Identifier.Text == invocationName);

                if (methodDeclaration == null)
                    continue; // skip if it doesn't exist in our inline list

                invocationMethodPairs.TryAdd(invocation, methodDeclaration);
            }

            var tempBody = method.Body;

            foreach (var (invocation, targetMethod) in invocationMethodPairs) {
                var targetMethodBody = targetMethod.Body;
                if (targetMethodBody == null)
                    continue;

                Debug.Log($"inv: {invocation.ToString()}, body: {targetMethodBody.ToString()}");

                var newStatements = targetMethodBody.Statements;

                tempBody = SyntaxFactory.Block(
                    tempBody.Statements.SelectMany<StatementSyntax, StatementSyntax>(stmt => {
                        if (stmt.Contains(invocation))
                            return newStatements;

                        return new[] { stmt };
                    })
                );

                tempBody = tempBody.ReplaceNode(invocation, newStatements);

                Debug.Log(tempBody.ToFullString());
            }

            method.WithBody(tempBody);

            return method;
        }

        // Probably shouldn't be using a syntax walker for this..
        public override SyntaxNode Visit(SyntaxNode node) {
            if (_isRoot) {
                _isRoot = false;
                _root = node as CompilationUnitSyntax;

                if (_root != null) {
                    var classDeclarations = _root.DescendantNodes().OfType<ClassDeclarationSyntax>();

                    foreach (var classDeclaration in classDeclarations) {
                        var inheritsFromBaseClass = classDeclaration.BaseList?.Types
                            .Any(baseType => baseType.ToString() == "UdonSharpBehaviour") ?? false;
                        if (!inheritsFromBaseClass) continue; // Skip if it's not a UdonSharpBehaviour

                        var inlineWholeClass = classDeclaration.AttributeLists.SelectMany(a => a.Attributes)
                            .Any(attr => attr.Name.ToString() == "Inline");

                        var allMethods = _root
                            .DescendantNodes()
                            .OfType<MethodDeclarationSyntax>()
                            .ToList();

                        _inlineAbleMethods = inlineWholeClass
                            ? allMethods
                            : _root
                                .DescendantNodes()
                                .OfType<MethodDeclarationSyntax>()
                                .Where(method => method.AttributeLists
                                    .SelectMany(a => a.Attributes)
                                    .Any(attr => attr.Name.ToString() == "Inline"))
                                .ToList();

                        if (_inlineAbleMethods.Count == 0)
                            continue;
                        
                        var newMethods = allMethods.Select(InlineMethodCalls).ToList();

                        var newClassDeclaration = classDeclaration;
                        for (int i = 0; i < newMethods.Count; i++) {
                            newClassDeclaration.ReplaceNode(allMethods[i], newMethods[i]);
                        }
                        
                        Debug.Log(newClassDeclaration.ToFullString());
                        
                        node = _root.ReplaceNode(classDeclaration, newClassDeclaration);
                    }
                }
                else {
                    Injections.PrintError("Root was null?");
                }
            }

            return base.Visit(node);
        }
    }
}