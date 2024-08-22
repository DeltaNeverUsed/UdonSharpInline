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

        private StatementSyntax CreateVariable(string identifier, string originalIdentifier) {
            return SyntaxFactory.ParseStatement($"var {identifier} = {originalIdentifier};");
        }

        public (MethodDeclarationSyntax, bool) InlineMethodCalls(MethodDeclarationSyntax method) {
            if (method.Body == null)
                return (method, false);

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

                if (methodDeclaration.Identifier.IsEquivalentTo(method.Identifier))
                    continue;

                invocationMethodPairs.TryAdd(invocation, methodDeclaration);
            }

            var tempBody = method.Body.Statements;

            var inlineCount = 0;

            foreach (var (invocation, targetMethod) in invocationMethodPairs) {
                var targetMethodBody = targetMethod.Body;
                if (targetMethodBody == null)
                    continue;

                var returnType = targetMethod.ReturnType;
                var hasReturnType = returnType.IsKind(SyntaxKind.PredefinedType) &&
                                    !((PredefinedTypeSyntax)returnType).Keyword.IsKind(SyntaxKind.VoidKeyword);
                
                while (tempBody.IndexOf(s => s.DescendantNodes().Contains(invocation)) != -1) {
                    targetMethodBody = targetMethod.Body;
                    var targetMethodStatements = targetMethodBody.Statements;
                    
                    // Create local declerations for function
                    var functionParams = targetMethod.ParameterList.Parameters.Select((t, i) =>
                            CreateVariable(t.Identifier.ToString(), invocation.ArgumentList.Arguments[i].ToString()))
                        .ToList();

                    targetMethodStatements = targetMethodStatements.InsertRange(0, functionParams);
                    //targetMethodStatements = targetMethodStatements.Add(endLabel);
                    targetMethodBody = targetMethodBody.WithStatements(targetMethodStatements);

                    var insertPlacement = tempBody.IndexOf(s => s.DescendantNodes().Contains(invocation));

                    if (hasReturnType) {
                        var returnSyntax =
                            targetMethodStatements.First(s => s is ReturnStatementSyntax) as ReturnStatementSyntax;
                        var returnNodes = returnSyntax.DescendantNodes();

                        var callStatement =
                            tempBody.First(s => s.DescendantNodes().Any(d => d.IsEquivalentTo(invocation)));
                        var callStatementNodes = callStatement.DescendantNodes().ToList();
                        var callStatementInv = callStatementNodes.First(s => s.IsEquivalentTo(invocation));
                        
                        // Get the parent statement of the invocation (usually an ExpressionStatementSyntax)
                        var parentStatement = callStatement.Ancestors().OfType<StatementSyntax>().First();

                        /*targetMethodBody.Statements.Where(node => !node.IsKind(SyntaxKind.ReturnKeyword));

                        // Get the parent block that contains the statement
                        
                        // Insert the new statement before the parent statement
                        switch (parentStatement) {
                            case BlockSyntax block:
                                var newBlock = block.WithStatements(
                                    block.Statements.Insert(block.Statements.IndexOf(s => s.IsEquivalentTo(callStatement)), targetMethodBody)
                                );
                                tempBody = method.Body.WithStatements(tempBody).ReplaceNode(parentStatement, newBlock).Statements;
                                break;
                            default:
                                Debug.LogError(parentStatement.Kind());
                                break;
                        }
                        
                        callStatement =
                            tempBody.First(s => s.DescendantNodes().Any(d => d.IsEquivalentTo(invocation)));
                        callStatementNodes = callStatement.DescendantNodes().ToList();
                        callStatementInv = callStatementNodes.First(s => s.IsEquivalentTo(invocation));
                        */

                        // Replace the old block with the new block in the root
                        
                        
                        //SyntaxFactory.Block(SyntaxFactory.EmptyStatement(), callStatement.ReplaceNode(callStatementInv, returnNodes.First()), )
                        
                        var newCallStatement = callStatement.ReplaceNode(callStatementInv, returnNodes.First());

                        insertPlacement = tempBody.IndexOf(callStatement);
                        tempBody = tempBody.Replace(callStatement, newCallStatement);
                        
                        var rewriter = new FunctionInserterRewriter(invocation, targetMethodBody);
                        tempBody = method.WithBody((BlockSyntax)rewriter.Visit(method.WithBody(method.Body.WithStatements(tempBody)))).Body.Statements;
                    }
                    
                    //var rewriter = new FunctionInserterRewriter(invocation, method);
                    /*if (targetMethodStatements.Any(s => s is ReturnStatementSyntax))
                        targetMethodStatements =
                            targetMethodStatements.Remove(
                                targetMethodStatements.First(s => s is ReturnStatementSyntax));

                    tempBody = tempBody.InsertRange(insertPlacement, targetMethodStatements);*/

                    inlineCount++;
                }
            }

            method = method.WithBody(method.Body.WithStatements(tempBody));
            return (method, inlineCount > 0);
        }

        // Probably shouldn't be using a syntax walker for this..
        public override SyntaxNode Visit(SyntaxNode node) {
            if (_isRoot) {
                _isRoot = false;
                _root = node as CompilationUnitSyntax;

                if (_root != null) {
                    var classDeclarations = _root.DescendantNodes().OfType<ClassDeclarationSyntax>();

                    foreach (var classDeclaration in classDeclarations) {
                        var inlineWholeClass = classDeclaration.AttributeLists.SelectMany(a => a.Attributes)
                            .Any(attr => attr.Name.ToString() == "Inline");

                        var allMethods = classDeclaration
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

                        if (inlineWholeClass)
                            _inlineAbleMethods = _inlineAbleMethods.Where(m =>
                                m.DescendantNodes().OfType<ReturnStatementSyntax>().Count() < 2).ToList();
                        else {
                            var tempCopy = _inlineAbleMethods.ToList();
                            foreach (var inlineAbleMethod in tempCopy.Where(inlineAbleMethod =>
                                         inlineAbleMethod.DescendantNodes().OfType<ReturnStatementSyntax>().Count() >
                                         1)) {
                                Debug.LogError("Can't inline methods with more than one Return!");
                                _inlineAbleMethods.Remove(inlineAbleMethod);
                            }
                        }

                        if (_inlineAbleMethods.Count == 0)
                            continue;

                        var newClassDeclaration = classDeclaration;
                        var continueInlining = true;
                        for (int i = 0; i < 3; i++) {
                            if (!continueInlining)
                                break;
                            continueInlining = false;
                            for (var index = 0; index < allMethods.Count; index++) {
                                var method = newClassDeclaration
                                    .DescendantNodes()
                                    .OfType<MethodDeclarationSyntax>()
                                    .ToList()[index];

                                var (inlined, success) = InlineMethodCalls(method);
                                continueInlining |= success;
                                if (success)
                                    newClassDeclaration = newClassDeclaration.ReplaceNode(method, inlined);
                            }
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