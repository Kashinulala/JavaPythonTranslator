using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System.Text;
using Antlr4.Runtime.Misc;


namespace LexerParserLibrary
{
    public class JavaSyntaxAnalyzer : JavaGrammarBaseListener
    {
        private readonly StringBuilder _output = new StringBuilder();
        private readonly StringBuilder _errors = new StringBuilder();
        private int _indentLevel = 0;
        private bool _hasErrors = false;

        public string GetAnalysisResult()
        {
            return _output.ToString();
        }

        public string GetErrors()
        {
            return _errors.ToString();
        }

        public bool HasErrors => _hasErrors;

        private void Indent()
        {
            _output.Append(new string(' ', _indentLevel * 4));
        }

        private void WriteLine(string text = "")
        {
            Indent();
            _output.AppendLine(text);
        }

        private void LogError(string message, ParserRuleContext context)
        {
            _hasErrors = true;
            int line = context.Start.Line;
            int column = context.Start.Column;
            _errors.AppendLine($"ERROR at line {line}, column {column}: {message}");
            WriteLine($"ERROR: {message}");
        }

        // Анализ классов
        public override void EnterClassDeclaration([NotNull] JavaGrammarParser.ClassDeclarationContext context)
        {
            string className = context.identifier().GetText();
            WriteLine($"ANALYZING CLASS: {className}");
            _indentLevel++;

            // Проверка корректности имени класса
            if (!char.IsUpper(className[0]))
            {
                LogError($"Class name '{className}' should start with uppercase letter", context);
            }
        }

        public override void ExitClassDeclaration([NotNull] JavaGrammarParser.ClassDeclarationContext context)
        {
            _indentLevel--;
            WriteLine($"END OF CLASS: {context.identifier().GetText()}");
        }

        // Анализ методов
        public override void EnterMethodDeclaratorRest([NotNull] JavaGrammarParser.MethodDeclaratorRestContext context)
        {
            var parentCtx = context.Parent as JavaGrammarParser.MethodOrFieldRestContext;
            if (parentCtx?.Parent is JavaGrammarParser.MethodOrFieldDeclContext methodDecl)
            {
                string methodName = methodDecl.identifier().GetText();
                string returnType = methodDecl.type().GetText();

                // Проверка корректности имени метода
                if (char.IsUpper(methodName[0]))
                {
                    LogError($"Method name '{methodName}' should start with lowercase letter", context);
                }

                // Анализ параметров
                var formalParams = context.formalParameters();
                WriteLine($"METHOD: {returnType} {methodName}()");
                _indentLevel++;

                if (formalParams != null && formalParams.formalParameterDecls() != null)
                {
                    WriteLine("Parameters:");
                    _indentLevel++;
                    foreach (var param in formalParams.formalParameterDecls().GetRuleContexts<JavaGrammarParser.FormalParameterDeclsContext>())
                    {
                        if (param.type() != null && param.formalParameterDeclsRest() != null)
                        {
                            string paramType = param.type().GetText();
                            // Получаем variableDeclaratorId из formalParameterDeclsRest
                            var declaratorRest = param.formalParameterDeclsRest();
                            string paramName = declaratorRest.variableDeclaratorId().GetText();

                            WriteLine($"- {paramType} {paramName}");
                        }
                    }
                    _indentLevel--;
                }
            }
        }

        public override void ExitMethodDeclaratorRest([NotNull] JavaGrammarParser.MethodDeclaratorRestContext context)
        {
            _indentLevel--;
        }

        // Анализ void методов
        public override void EnterVoidMethodDeclaratorRest([NotNull] JavaGrammarParser.VoidMethodDeclaratorRestContext context)
        {
            var parentCtx = context.Parent as JavaGrammarParser.VoidMethodMemberContext;
            if (parentCtx != null)
            {
                string methodName = parentCtx.identifier().GetText();

                // Проверка корректности имени метода
                if (char.IsUpper(methodName[0]))
                {
                    LogError($"Method name '{methodName}' should start with lowercase letter", context);
                }

                WriteLine($"VOID METHOD: void {methodName}()");
                _indentLevel++;
            }
        }

        public override void ExitVoidMethodDeclaratorRest([NotNull] JavaGrammarParser.VoidMethodDeclaratorRestContext context)
        {
            _indentLevel--;
        }

        // Анализ оператора if
        public override void EnterIfStatement([NotNull] JavaGrammarParser.IfStatementContext context)
        {
            WriteLine("IF STATEMENT");
            _indentLevel++;
        }

        public override void ExitIfStatement([NotNull] JavaGrammarParser.IfStatementContext context)
        {
            _indentLevel--;
            WriteLine("END IF");
        }

        // Анализ оператора for
        public override void EnterForStatement([NotNull] JavaGrammarParser.ForStatementContext context)
        {
            WriteLine("FOR LOOP");
            _indentLevel++;
        }

        public override void ExitForStatement([NotNull] JavaGrammarParser.ForStatementContext context)
        {
            _indentLevel--;
            WriteLine("END FOR");
        }

        // Анализ оператора while
        public override void EnterWhileStatement([NotNull] JavaGrammarParser.WhileStatementContext context)
        {
            WriteLine("WHILE LOOP");
            _indentLevel++;
        }

        public override void ExitWhileStatement([NotNull] JavaGrammarParser.WhileStatementContext context)
        {
            _indentLevel--;
            WriteLine("END WHILE");
        }

        // Анализ оператора switch
        public override void EnterSwitchStatement([NotNull] JavaGrammarParser.SwitchStatementContext context)
        {
            WriteLine("SWITCH STATEMENT");
            _indentLevel++;
        }

        public override void ExitSwitchStatement([NotNull] JavaGrammarParser.SwitchStatementContext context)
        {
            _indentLevel--;
            WriteLine("END SWITCH");
        }

        // Анализ выражений присваивания
        public override void EnterAssignmentExpression([NotNull] JavaGrammarParser.AssignmentExpressionContext context)
        {
            string left = context.expression(0).GetText();
            string right = context.expression(1).GetText();
            WriteLine($"ASSIGNMENT: {left} = {right}");
        }

        // Анализ вызовов методов и доступа к полям
        public override void EnterMethodOrFieldSelector([NotNull] JavaGrammarParser.MethodOrFieldSelectorContext context)
        {
            string identifier = context.identifier().GetText();
            if (context.arguments() != null)
            {
                WriteLine($"METHOD CALL: {identifier}()");
            }
            else
            {
                WriteLine($"FIELD ACCESS: {identifier}");
            }
        }

        // Анализ создания объектов
        public override void EnterNewCreatorPrimary([NotNull] JavaGrammarParser.NewCreatorPrimaryContext context)
        {
            if (context.creator() != null && context.creator().createdName() != null)
            {
                string className = context.creator().createdName().GetText();
                WriteLine($"OBJECT CREATION: new {className}");
            }
        }

        // Анализ импортов
        public override void EnterImportDeclaration([NotNull] JavaGrammarParser.ImportDeclarationContext context)
        {
            string imported = context.qualifiedIdentifier().GetText();
            bool isStatic = context.STATIC() != null;
            bool isWildcard = context.DOT() != null && context.GetChild(context.ChildCount - 2).GetText() == "*";

            string importType = isStatic ? "STATIC IMPORT" : "IMPORT";
            string wildcard = isWildcard ? ".*" : "";
            WriteLine($"{importType}: {imported}{wildcard}");
        }

        // Анализ полей
        public override void EnterFieldDeclaratorsRest([NotNull] JavaGrammarParser.FieldDeclaratorsRestContext context)
        {
            var parentCtx = context.Parent as JavaGrammarParser.MethodOrFieldRestContext;
            if (parentCtx?.Parent is JavaGrammarParser.MethodOrFieldDeclContext fieldDecl)
            {
                string fieldType = fieldDecl.type().GetText();
                string fieldName = fieldDecl.identifier().GetText();

                // Проверка корректности имени поля
                if (!char.IsLower(fieldName[0]) && !fieldName.StartsWith("_"))
                {
                    LogError($"Field name '{fieldName}' should start with lowercase letter", context);
                }

                WriteLine($"FIELD: {fieldType} {fieldName}");
            }
        }

        // Анализ статических блоков
        public override void EnterStaticBlockClassBodyDeclaration([NotNull] JavaGrammarParser.StaticBlockClassBodyDeclarationContext context)
        {
            WriteLine("STATIC INITIALIZATION BLOCK");
            _indentLevel++;
        }

        public override void ExitStaticBlockClassBodyDeclaration([NotNull] JavaGrammarParser.StaticBlockClassBodyDeclarationContext context)
        {
            _indentLevel--;
            WriteLine("END STATIC BLOCK");
        }

        // Обработка ошибок разбора
        public override void VisitErrorNode([NotNull] IErrorNode node)
        {
            _hasErrors = true;
            _errors.AppendLine($"SYNTAX ERROR at line {node.Symbol.Line}, column {node.Symbol.Column}: {node.GetText()}");
        }

        // Генерация итогового отчета
        public void GenerateReport()
        {
            string a = _hasErrors ? "YES" : "NO";
            WriteLine("\n===== SYNTAX ANALYSIS REPORT =====");
            WriteLine($"Errors found: {a}");

            if (_hasErrors)
            {
                WriteLine("\nERRORS:");
                WriteLine(_errors.ToString());
            }
        }
    }
}