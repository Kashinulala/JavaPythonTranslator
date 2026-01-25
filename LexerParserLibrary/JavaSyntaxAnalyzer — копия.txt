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

        public override void ExitNewCreatorPrimary([NotNull] JavaGrammarParser.NewCreatorPrimaryContext context)
        {
            _indentLevel--;
        }

        public override void EnterCreator([NotNull] JavaGrammarParser.CreatorContext context)
        {
            WriteLine("CREATOR CONTEXT");
            _indentLevel++;
        }

        public override void ExitCreator([NotNull] JavaGrammarParser.CreatorContext context)
        {
            _indentLevel--;
        }

        public override void EnterCreatedName([NotNull] JavaGrammarParser.CreatedNameContext context)
        {
            var identifiers = context.identifier();
            if (identifiers != null && identifiers.Length > 0)
            {
                // Собираем полное имя класса из всех идентификаторов
                string fullName = string.Join(".", identifiers.Select(id => id.GetText()));
                WriteLine($"CREATING INSTANCE OF: {fullName}");
            }
        }

        public override void ExitCreatedName([NotNull] JavaGrammarParser.CreatedNameContext context)
        {
        }

        public override void EnterClassCreatorRest([NotNull] JavaGrammarParser.ClassCreatorRestContext context)
        {
            WriteLine("CLASS CREATOR REST (arguments and optional class body)");
            _indentLevel++;

            // Обработка аргументов конструктора
            if (context.arguments() != null)
            {
                WriteLine("CONSTRUCTOR ARGUMENTS:");
                _indentLevel++;
            }
        }

        public override void ExitClassCreatorRest([NotNull] JavaGrammarParser.ClassCreatorRestContext context)
        {
            if (context.arguments() != null)
            {
                _indentLevel--;
            }
            _indentLevel--;
        }

        public override void EnterArrayCreatorRest([NotNull] JavaGrammarParser.ArrayCreatorRestContext context)
        {
            WriteLine("ARRAY CREATOR");
            _indentLevel++;
        }

        public override void ExitArrayCreatorRest([NotNull] JavaGrammarParser.ArrayCreatorRestContext context)
        {
            _indentLevel--;
        }

        // Добавление методов для обработки дженериков
        public override void EnterTypeArgumentsOrDiamond([NotNull] JavaGrammarParser.TypeArgumentsOrDiamondContext context)
        {
            if (context.LT() != null && context.GT() != null && context.typeArguments() == null)
            {
                WriteLine("GENERIC TYPE WITH DIAMOND OPERATOR: <>");
            }
            else if (context.typeArguments() != null)
            {
                WriteLine("GENERIC TYPE ARGUMENTS");
                _indentLevel++;
            }
        }

        public override void ExitTypeArgumentsOrDiamond([NotNull] JavaGrammarParser.TypeArgumentsOrDiamondContext context)
        {
            if (context.typeArguments() != null)
            {
                _indentLevel--;
            }
        }

        // Добавление методов для обработки приведения типов
        public override void EnterCastExpression([NotNull] JavaGrammarParser.CastExpressionContext context)
        {
            if (context.type() != null)
            {
                string castType = context.type().GetText();
                WriteLine($"CAST EXPRESSION: ({castType})");
                _indentLevel++;
            }
        }

        public override void ExitCastExpression([NotNull] JavaGrammarParser.CastExpressionContext context)
        {
            if (context.type() != null)
            {
                _indentLevel--;
            }
        }

        // Добавление методов для обработки префиксных операторов
        public override void EnterPrefixExpression([NotNull] JavaGrammarParser.PrefixExpressionContext context)
        {
            if (context.prefixOp() != null)
            {
                string operatorText = context.prefixOp().GetText();
                WriteLine($"PREFIX OPERATION: {operatorText}");
                _indentLevel++;
            }
        }

        public override void ExitPrefixExpression([NotNull] JavaGrammarParser.PrefixExpressionContext context)
        {
            if (context.prefixOp() != null)
            {
                _indentLevel--;
            }
        }

        // Добавление методов для обработки инициализаторов массивов
        public override void EnterArrayInitializer([NotNull] JavaGrammarParser.ArrayInitializerContext context)
        {
            WriteLine("ARRAY INITIALIZER: {");
            _indentLevel++;
        }

        public override void ExitArrayInitializer([NotNull] JavaGrammarParser.ArrayInitializerContext context)
        {
            _indentLevel--;
            WriteLine("}");
        }

        // Добавление методов для обработки switch более детально
        public override void EnterSwitchBlockStatementGroups([NotNull] JavaGrammarParser.SwitchBlockStatementGroupsContext context)
        {
            WriteLine("SWITCH BLOCK STATEMENT GROUPS");
            _indentLevel++;
        }

        public override void ExitSwitchBlockStatementGroups([NotNull] JavaGrammarParser.SwitchBlockStatementGroupsContext context)
        {
            _indentLevel--;
        }

        public override void EnterSwitchBlockStatementGroup([NotNull] JavaGrammarParser.SwitchBlockStatementGroupContext context)
        {
            WriteLine("SWITCH BLOCK STATEMENT GROUP");
            _indentLevel++;
        }

        public override void ExitSwitchBlockStatementGroup([NotNull] JavaGrammarParser.SwitchBlockStatementGroupContext context)
        {
            _indentLevel--;
        }

        public override void EnterSwitchLabels([NotNull] JavaGrammarParser.SwitchLabelsContext context)
        {
            WriteLine("SWITCH LABELS");
            _indentLevel++;
        }

        public override void ExitSwitchLabels([NotNull] JavaGrammarParser.SwitchLabelsContext context)
        {
            _indentLevel--;
        }

        public override void EnterEnumConstantName([NotNull] JavaGrammarParser.EnumConstantNameContext context)
        {
            if (context.identifier() != null)
            {
                string enumName = context.identifier().GetText();
                WriteLine($"ENUM CONSTANT: {enumName}");
            }
        }

        // Добавление методов для обработки циклов for более детально
        public override void EnterForVarControl([NotNull] JavaGrammarParser.ForVarControlContext context)
        {
            WriteLine("FOR VARIABLE CONTROL");
            _indentLevel++;

            if (context.type() != null && context.variableDeclaratorId() != null)
            {
                string varType = context.type().GetText();
                string varName = context.variableDeclaratorId().GetText();
                WriteLine($"LOOP VARIABLE: {varType} {varName}");
            }
        }

        public override void ExitForVarControl([NotNull] JavaGrammarParser.ForVarControlContext context)
        {
            _indentLevel--;
        }

        public override void EnterForVarControlRest([NotNull] JavaGrammarParser.ForVarControlRestContext context)
        {
            if (context.COLON() != null)
            {
                WriteLine("ENHANCED FOR LOOP (for-each)");
            }
            else
            {
                WriteLine("TRADITIONAL FOR LOOP");
            }
        }

        public override void EnterArraySelector([NotNull] JavaGrammarParser.ArraySelectorContext context)
        {
            WriteLine("ARRAY ACCESS");
            _indentLevel++;
        }

        public override void ExitArraySelector([NotNull] JavaGrammarParser.ArraySelectorContext context)
        {
            _indentLevel--;
        }

        // Добавление методов для обработки суффиксов идентификаторов
        public override void EnterClassLiteralSuffix([NotNull] JavaGrammarParser.ClassLiteralSuffixContext context)
        {
            WriteLine("CLASS LITERAL (e.g., int[].class)");
        }

        public override void EnterArrayAccessSuffix([NotNull] JavaGrammarParser.ArrayAccessSuffixContext context)
        {
            WriteLine("ARRAY ACCESS SUFFIX");
            _indentLevel++;
        }

        public override void ExitArrayAccessSuffix([NotNull] JavaGrammarParser.ArrayAccessSuffixContext context)
        {
            _indentLevel--;
        }

        public override void EnterArgumentsSuffix([NotNull] JavaGrammarParser.ArgumentsSuffixContext context)
        {
            WriteLine("METHOD ARGUMENTS SUFFIX");
            _indentLevel++;
        }

        public override void ExitArgumentsSuffix([NotNull] JavaGrammarParser.ArgumentsSuffixContext context)
        {
            _indentLevel--;
        }

        // Добавление методов для обработки выражений в скобках
        public override void EnterParExpression([NotNull] JavaGrammarParser.ParExpressionContext context)
        {
            WriteLine("PARENTHESIZED EXPRESSION");
            _indentLevel++;
        }

        public override void ExitParExpression([NotNull] JavaGrammarParser.ParExpressionContext context)
        {
            _indentLevel--;
        }

        // Добавление методов для обработки литералов и примитивных типов
        public override void EnterBasicType([NotNull] JavaGrammarParser.BasicTypeContext context)
        {
            string typeName = context.GetText().ToLower();
            WriteLine($"BASIC TYPE: {typeName}");
        }

        public override void EnterReferenceType([NotNull] JavaGrammarParser.ReferenceTypeContext context)
        {
            var identifiers = context.identifier();
            if (identifiers != null && identifiers.Length > 0)
            {
                // Собираем полное имя типа из всех идентификаторов (например, "java.util.List")
                string fullName = string.Join(".", identifiers.Select(id => id.GetText()));
                WriteLine($"REFERENCE TYPE: {fullName}");

                if (context.typeArguments() != null)
                {
                    WriteLine("WITH GENERIC ARGUMENTS");
                    _indentLevel++;
                }
            }
        }

        public override void ExitReferenceType([NotNull] JavaGrammarParser.ReferenceTypeContext context)
        {
            if (context.typeArguments() != null)
            {
                _indentLevel--;
            }
        }

        // Добавление методов для обработки префиксных и постфиксных операторов
        public override void EnterPrefixOp([NotNull] JavaGrammarParser.PrefixOpContext context)
        {
            string op = context.GetText();
            WriteLine($"PREFIX OPERATOR: {op}");
        }

        public override void EnterPostfixOp([NotNull] JavaGrammarParser.PostfixOpContext context)
        {
            string op = context.GetText();
            WriteLine($"POSTFIX OPERATOR: {op}");
        }

        // Добавление методов для обработки инфиксных операторов
        public override void EnterInfixOp([NotNull] JavaGrammarParser.InfixOpContext context)
        {
            string op = context.GetText();
            WriteLine($"INFIX OPERATOR: {op}");
        }

        // Добавление методов для обработки анонимных классов
        public override void EnterClassBody([NotNull] JavaGrammarParser.ClassBodyContext context)
        {
            // Проверяем, не является ли это анонимным классом
            var parent = context.Parent;
            while (parent != null)
            {
                if (parent is JavaGrammarParser.ClassCreatorRestContext)
                {
                    WriteLine("ANONYMOUS CLASS BODY");
                    break;
                }
                parent = parent.Parent;
            }

            if (parent == null)
            {
                WriteLine("CLASS BODY");
            }

            _indentLevel++;
        }

        public override void ExitClassBody([NotNull] JavaGrammarParser.ClassBodyContext context)
        {
            _indentLevel--;
            WriteLine("END CLASS BODY");
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