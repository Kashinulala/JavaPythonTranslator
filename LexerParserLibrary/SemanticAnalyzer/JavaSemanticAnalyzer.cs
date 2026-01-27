using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace LexerParserLibrary.SemanticAnalyzer
{
    public class SemanticAnalyzer : JavaGrammarBaseVisitor<object>
    {
        private readonly SymbolTable _symbolTable = new SymbolTable();
        private readonly List<SemanticException> _errors = new List<SemanticException>();
        private readonly List<SemanticException> _warnings = new List<SemanticException>();

        public IEnumerable<SemanticException> Errors => _errors;
        public bool HasErrors => _errors.Count > 0;
        public IEnumerable<SemanticException> Warnings => _warnings;
        public bool HasWarnings => _warnings.Count > 0;

        // Анализ объявления переменных
        public override object VisitVariableDeclarator(JavaGrammarParser.VariableDeclaratorContext context)
        {
            string varName = context.identifier().GetText();
            string varType = "unknown";
            bool isFinal = false;
            bool isStatic = false;

            var parentCtx = context.Parent;

            // Обработка локальных переменных
            if (parentCtx is JavaGrammarParser.VariableDeclaratorsContext varDecls)
            {
                // Проверяем, является ли родитель LocalVariableDeclarationStatementContext (обычный случай: int x = 10;)
                var localVarDeclStmt = GetParentOfType<JavaGrammarParser.LocalVariableDeclarationStatementContext>(varDecls);
                if (localVarDeclStmt != null)
                {
                    varType = GetTypeName(localVarDeclStmt.type());
                }
                // Если нет, проверяем, является ли родитель LocalVariableDeclarationContext (для forInit: int i = 0)
                else
                {
                    var localVarDecl = GetParentOfType<JavaGrammarParser.ForDeclarationContextContext>(varDecls);
                    if (localVarDecl != null)
                    {
                        varType = GetTypeName(localVarDecl.type());
                    }
                    // Если и LocalVariableDeclarationContext не найден, оставляем varType = "unknown"
                    // или можно добавить логирование для отладки
                    // else { /* Возможно, ошибка в грамматике или дереве */ }
                }
            }
            // Обработка полей класса (оставляем как есть)
            else if (parentCtx is JavaGrammarParser.FieldDeclaratorsRestContext fieldRest)
            {
                var fieldParent = fieldRest.Parent as JavaGrammarParser.MethodOrFieldRestContext;
                if (fieldParent != null)
                {
                    var methodOrFieldParent = fieldParent.Parent as JavaGrammarParser.MethodOrFieldDeclContext;
                    if (methodOrFieldParent != null)
                    {
                        varType = GetTypeName(methodOrFieldParent.type());

                        // Поиск модификаторов в правильном месте
                        var memberDecl = GetParentOfType<JavaGrammarParser.MemberClassBodyDeclarationContext>(methodOrFieldParent);
                        if (memberDecl != null)
                        {
                            foreach (var modifier in memberDecl.modifier())
                            {
                                if (modifier.GetText() == "final") isFinal = true;
                                if (modifier.GetText() == "static") isStatic = true;
                            }
                        }
                    }
                }
            }

            try
            {
                // Проверка дублирования имен
                if (_symbolTable.IsDeclaredInCurrentScope(varName))
                {
                    ReportError($"Variable '{varName}' is already declared in this scope", context);
                }
                else
                {
                    _symbolTable.Declare(varName, varType, isFinal, isStatic);
                }

                // Проверка инициализации final переменных
                if (context.variableDeclaratorRest() != null &&
                    context.variableDeclaratorRest().variableInitializer() != null)
                {
                    Visit(context.variableDeclaratorRest().variableInitializer());
                    var symbol = _symbolTable.GetSymbol(varName);
                    if (symbol != null)
                    {
                        symbol.IsInitialized = true;
                    }
                }
            }
            catch (SemanticException ex)
            {
                _errors.Add(ex);
            }

            return null;
        }

        // Переопределите VisitPrimaryExpression
        public override object VisitPrimaryExpression(JavaGrammarParser.PrimaryExpressionContext context)
        {
            // PrimaryExpressionContext содержит expression1()
            // Мы должны вызвать GetExpression1Type для этого expression1
            var expr1 = context.expression1();
            if (expr1 != null)
            {
                string exprType = GetExpression1Type(expr1);
                // Возвращаем тип выражения
                return exprType;
            }

            // Если expression1 отсутствует, возвращаем unknown
            return "unknown";
        }

        // Переопределите VisitSimpleExpression2
        public override object VisitSimpleExpression2(JavaGrammarParser.SimpleExpression2Context context)
        {
            // SimpleExpression2Context содержит expression2()
            // Мы должны вызвать GetExpression2Type для этого expression2
            var expr2 = context.expression2();
            if (expr2 != null)
            {
                string exprType = GetExpression2Type(expr2);
                // Возвращаем тип выражения
                return exprType;
            }

            // Если expression2 отсутствует, возвращаем unknown
            return "unknown";
        }

        // Анализ выражений присваивания
        public override object VisitAssignmentExpression(JavaGrammarParser.AssignmentExpressionContext context)
        {
            var left = context.expression(0);
            string varName = null;

            // Извлечение имени переменной из левой части присваивания
            if (left is JavaGrammarParser.PrimaryExpressionContext primaryExpr)
            {
                var expr1 = primaryExpr.expression1();
                if (expr1 != null)
                {
                    if (expr1 is JavaGrammarParser.SimpleExpression2Context simpleExpr2)
                    {
                        var expr2 = simpleExpr2.expression2();
                        if (expr2 is JavaGrammarParser.PostfixExpressionContext postfixExpr)
                        {
                            var primary = postfixExpr.primary();
                            if (primary is JavaGrammarParser.IdentifierPrimaryContext idPrimary &&
                                idPrimary.identifier().Length > 0)
                            {
                                varName = idPrimary.identifier()[0].GetText();
                            }
                        }
                    }
                }
            }

            if (varName != null)
            {
                var symbol = _symbolTable.GetSymbol(varName);
                if (symbol == null)
                {
                    ReportError($"Variable '{varName}' is not declared", left);
                }
                else if (symbol.IsFinal && symbol.IsInitialized)
                {
                    ReportError($"Final variable '{varName}' cannot be reassigned", left);
                }
            }

            return base.VisitAssignmentExpression(context);
        }

        // Анализ инфиксных выражений (арифметические и логические операторы)
        public override object VisitInfixExpression(JavaGrammarParser.InfixExpressionContext context)
        {
            var expr2List = context.expression2();
            if (expr2List == null || expr2List.Length < 2)
                return base.VisitInfixExpression(context);

            string leftType = GetExpression2Type(expr2List[0]);
            string rightType = GetExpression2Type(expr2List[1]);

            var infixOps = context.infixOp();
            foreach (var op in infixOps)
            {
                string operatorText = op.GetText();

                switch (operatorText)
                {
                    case "&&":
                    case "||":
                        if (leftType != "boolean" || rightType != "boolean")
                        {
                            ReportError($"Logical operator '{operatorText}' requires boolean operands", op);
                        }
                        break;

                    case "==":
                    case "!=":
                        if (!AreTypesCompatible(leftType, rightType))
                        {
                            ReportError($"Incompatible types for comparison: {leftType} and {rightType}", op);
                        }
                        break;

                    case "<":
                    case ">":
                    case "<=":
                    case ">=":
                        if (!IsNumericType(leftType) || !IsNumericType(rightType))
                        {
                            ReportError($"Relational operator '{operatorText}' requires numeric operands", op);
                        }
                        break;

                    case "+":
                        if (!(IsNumericType(leftType) && IsNumericType(rightType)) &&
                            !(leftType == "String" || rightType == "String"))
                        {
                            ReportError($"Operator '+' not applicable to types {leftType} and {rightType}", op);
                        }
                        break;

                    case "-":
                    case "*":
                    case "/":
                    case "%":
                        if (!IsNumericType(leftType) || !IsNumericType(rightType))
                        {
                            ReportError($"Arithmetic operator '{operatorText}' requires numeric operands", op);
                        }
                        break;
                }
            }

            return base.VisitInfixExpression(context);
        }

        // Анализ префиксных выражений (!a, +a, -a)
        public override object VisitPrefixExpression(JavaGrammarParser.PrefixExpressionContext context)
        {
            var prefixOp = context.prefixOp();
            var expr2 = context.expression2();

            if (prefixOp != null && expr2 != null)
            {
                string operatorText = prefixOp.GetText();
                string operandType = GetExpression2Type(expr2);

                switch (operatorText)
                {
                    case "!":
                        if (operandType != "boolean")
                        {
                            ReportError($"Operator '!' requires boolean operand", prefixOp);
                        }
                        break;
                    case "+":
                    case "-":
                        if (!IsNumericType(operandType))
                        {
                            ReportError($"Unary operator '{operatorText}' requires numeric operand", prefixOp);
                        }
                        break;
                }
            }

            return base.VisitPrefixExpression(context);
        }

        // Анализ постфиксных выражений (a++, a--)
        public override object VisitPostfixExpression(JavaGrammarParser.PostfixExpressionContext context)
        {
            var primary = context.primary();
            var postfixOp = context.postfixOp();

            if (postfixOp != null && primary is JavaGrammarParser.IdentifierPrimaryContext idPrimary &&
                idPrimary.identifier().Length > 0)
            {
                string varName = idPrimary.identifier()[0].GetText();
                var symbol = _symbolTable.GetSymbol(varName);

                if (symbol == null)
                {
                    ReportError($"Variable '{varName}' is not declared", context);
                }
                else if (symbol.IsFinal)
                {
                    ReportError($"Cannot modify final variable '{varName}'", context);
                }
                else if (!IsNumericType(symbol.Type))
                {
                    ReportError($"Postfix operator '{postfixOp.GetText()}' requires numeric operand", context);
                }
            }

            return base.VisitPostfixExpression(context);
        }

        // Анализ условного оператора if
        public override object VisitIfStatement(JavaGrammarParser.IfStatementContext context)
        {
            var conditionExpr = context.parExpression().expression();
            if (conditionExpr != null)
            {
                string conditionType = GetExpressionType(conditionExpr);
                if (conditionType != "boolean")
                {
                    ReportError($"If condition must be of boolean type, found: {conditionType}", context);
                }
            }

            return base.VisitIfStatement(context);
        }

        // Анализ цикла while
        public override object VisitWhileStatement(JavaGrammarParser.WhileStatementContext context)
        {
            var conditionExpr = context.parExpression().expression();
            if (conditionExpr != null)
            {
                string conditionType = GetExpressionType(conditionExpr);
                if (conditionType != "boolean")
                {
                    ReportError($"While condition must be of boolean type, found: {conditionType}", context);
                }
            }

            return base.VisitWhileStatement(context);
        }

        // Анализ оператора switch
        public override object VisitSwitchStatement(JavaGrammarParser.SwitchStatementContext context)
        {
            string switchType = GetExpressionType(context.parExpression().expression());
            if (!IsValidSwitchType(switchType))
            {
                ReportError($"Invalid type for switch expression: {switchType}. Valid types are: int, char, String", context);
            }

            return base.VisitSwitchStatement(context);
        }

        // Анализ создания объектов и коллекций
        public override object VisitNewCreatorPrimary(JavaGrammarParser.NewCreatorPrimaryContext context)
        {
            if (context.creator() != null && context.creator().createdName() != null)
            {
                var createdName = context.creator().createdName();
                if (createdName.identifier().Length > 0)
                {
                    string className = createdName.identifier()[0].GetText();

                    // Проверяем, существует ли класс
                    if (!_symbolTable.IsClass(className))
                    {
                        ReportError($"Class '{className}' is not declared", context);
                    }
                }
            }

            return base.VisitNewCreatorPrimary(context);
        }

        private string GetExpressionType(JavaGrammarParser.ExpressionContext context)
        {
            if (context == null) return "unknown";

            // ExpressionContext может быть AssignmentExpressionContext или PrimaryExpressionContext
            if (context is JavaGrammarParser.AssignmentExpressionContext assignmentExpr)
            {
                // Тип присваивания - тип правой части (последнего выражения в списке, если их несколько)
                var expressions = assignmentExpr.expression();
                if (expressions != null && expressions.Length > 0)
                {
                    // Правая часть присваивания - это последний элемент в массиве expression()
                    // assignmentExpr.expression(0) - левая часть
                    // assignmentExpr.expression(1) - правая часть (для ASSIGN)
                    if (expressions.Length >= 2)
                    {
                        return GetExpressionType(expressions[expressions.Length - 1]);
                    }
                    else if (expressions.Length == 1)
                    {
                        // Это может быть случай, если выражение только одно (например, вложенный вызов)
                        // Но для ASSIGN всегда должно быть 2: лево = право
                        // Если пришло только одно выражение, возможно, это не ASSIGN, а просто PrimaryExpression
                        // Это маловероятно для AssignmentExpression, но на всякий случай:
                        return GetExpressionType(expressions[0]);
                    }
                }
                return "unknown";
            }
            // Проверяем PrimaryExpression (это основной случай для выражений, не являющихся присваиванием)
            else if (context is JavaGrammarParser.PrimaryExpressionContext primaryExpr)
            {
                var expr1 = primaryExpr.expression1();
                if (expr1 != null)
                {
                    return GetExpression1Type(expr1);
                }
            }

            return "unknown";
        }

        private string GetExpression1Type(JavaGrammarParser.Expression1Context expr1)
        {
            if (expr1 == null) return "unknown";

            // Expression1 может быть InfixExpressionContext или SimpleExpression2Context
            // Проверяем, является ли выражение1 инфиксным (операции)
            if (expr1 is JavaGrammarParser.InfixExpressionContext infixExpr)
            {
                // Дополнительная проверка: если нет операторов, это может быть просто SimpleExpression2
                var operators = infixExpr.infixOp();
                if (operators != null && operators.Length > 0)
                {
                    // Есть хотя бы один оператор - действительно инфиксное выражение
                    return GetInfixExpressionType(infixExpr);
                }
                else
                {
                    // Нет операторов - это скорее всего просто expression2
                    // Проверим, есть ли хотя бы один expression2
                    var operands = infixExpr.expression2();
                    if (operands != null && operands.Length == 1)
                    {
                        // Это может быть эквивалент SimpleExpression2 -> expression2
                        // Вызовем GetExpression2Type для этого единственного выражения
                        return GetExpression2Type(operands[0]);
                    }
                    // Если operands.Length != 1, это странная ситуация
                }
            }
            // Проверяем, является ли выражение1 простым выражением2 (обычно первичное выражение или унарные/постфиксные операции)
            else if (expr1 is JavaGrammarParser.SimpleExpression2Context simpleExpr2)
            {
                var expr2 = simpleExpr2.expression2();
                if (expr2 != null)
                {
                    // Теперь expr2 может быть PrefixExpression, PostfixExpression или другими
                    // Вызываем GetExpression2Type для обработки expr2
                    return GetExpression2Type(expr2);
                }
            }

            return "unknown";
        }

        private string GetExpression2Type(JavaGrammarParser.Expression2Context context)
        {
            // Expression2 может быть:
            // 1. PostfixExpressionContext (с primary внутри)
            // 2. PrefixExpressionContext (с primary внутри)
            // Проверяем PostfixExpressionContext - это основной случай для Expression2
            if (context is JavaGrammarParser.PostfixExpressionContext postfixExpr)
            {
                // Вызываем отдельный метод для обработки PostfixExpression
                // Этот метод должен содержать всю логику для PostfixExpression, включая primary, selector, postfixOp
                return GetPostfixExpressionType(postfixExpr);
            }
            // Проверяем PrefixExpressionContext
            else if (context is JavaGrammarParser.PrefixExpressionContext prefixExpr)
            {
                return GetPrefixExpressionType(prefixExpr);
            }

            // Если ни одно из условий не сработало, возвращаем unknown
            return "unknown";
        }

        // Вспомогательный метод для получения типа
        private string GetTypeName(JavaGrammarParser.TypeContext context)
        {
            if (context == null) return "unknown";

            if (context is JavaGrammarParser.BasicTypeTypeContext basicTypeCtx &&
                basicTypeCtx.basicType() != null)
            {
                return basicTypeCtx.basicType().GetText().ToLower();
            }

            if (context is JavaGrammarParser.ReferenceTypeTypeContext refTypeCtx)
            {
                var refType = refTypeCtx.referenceType();
                if (refType != null && refType.identifier().Length > 0)
                {
                    return refType.identifier()[0].GetText();
                }
            }

            return "unknown";
        }

        private string GetInfixExpressionType(JavaGrammarParser.InfixExpressionContext infixExpr)
        {
            if (infixExpr == null) return "unknown";

            var operators = infixExpr.infixOp();
            var operands = infixExpr.expression2();

            // Проверяем, есть ли хотя бы один оператор и два операнда
            if (operators.Length == 0 || operands.Length < 2)
            {
                // Это странная ситуация, возможно, выражение вроде "x"
                // Но это не InfixExpression, если нет операторов.
                // Если длина operands > 1, а операторов нет, это ошибка грамматики.
                if (operands.Length == 1)
                {
                    // Это может быть не InfixExpression вовсе, а SimpleExpression2 или что-то другое.
                    // Хотя грамматика выражает это как InfixExpression даже с 1 операндом и 0 операторов.
                    // В этом случае, просто возвращаем тип первого операнда.
                    return GetExpression2Type(operands[0]);
                }
                return "unknown";
            }

            // Начинаем с левого операнда
            string resultType = GetExpression2Type(operands[0]);

            // Проверяем каждый оператор и соответствующий правый операнд
            for (int i = 0; i < operators.Length; i++)
            {
                if (i + 1 >= operands.Length)
                {
                    // Ошибка: оператор без соответствующего правого операнда
                    ReportError("Infix expression has operator without right operand", infixExpr);
                    return "unknown";
                }

                var op = operators[i].GetText();
                string rightOperandType = GetExpression2Type(operands[i + 1]);

                // Проверяем, совместимы ли типы для данного оператора
                // Операторы сравнения
                if (new[] { "<", ">", "<=", ">=", "==", "!=", "&&", "||" }.Contains(op))
                {
                    // Проверяем, являются ли оба операнда числовыми для числовых операторов сравнения
                    if (new[] { "<", ">", "<=", ">=", "==", "!=" }.Contains(op))
                    {
                        if (!IsNumericType(resultType) || !IsNumericType(rightOperandType))
                        {
                            ReportError($"Relational operator '{op}' requires numeric operands, found: {resultType} and {rightOperandType}", infixExpr);
                            return "unknown";
                        }
                    }
                    // Проверяем, являются ли оба операнда булевыми для логических операторов сравнения
                    else if (new[] { "&&", "||" }.Contains(op))
                    {
                        if (resultType != "boolean" || rightOperandType != "boolean")
                        {
                            ReportError($"Logical operator '{op}' requires boolean operands, found: {resultType} and {rightOperandType}", infixExpr);
                            return "unknown";
                        }
                    }
                    // Результат операции сравнения - boolean
                    resultType = "boolean";
                }
                // Арифметические/логические/другие бинарные операции
                else if (new[] { "+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", ">>>" }.Contains(op))
                {
                    if (!IsNumericType(resultType) || !IsNumericType(rightOperandType))
                    {
                        ReportError($"Binary operator '{op}' requires numeric operands, found: {resultType} and {rightOperandType}", infixExpr);
                        return "unknown";
                    }
                    // Результат арифметической операции зависит от типов операндов
                    resultType = GetArithmeticResultType(resultType, rightOperandType);
                }
                // Операторы присваивания (редко встречаются внутри других выражений)
                else if (new[] { "=", "+=", "-=", "*=", "/=", "%=" }.Contains(op))
                {
                    // Для цепочек вроде a = b = c, тип правой части (c) должен быть совместим с типом левой (b, a)
                    // Это сложнее, но для простоты, результат присваивания - тип правого операнда
                    if (!AreTypesCompatible(resultType, rightOperandType))
                    {
                        ReportError($"Assignment operator '{op}' requires compatible types, found: {resultType} and {rightOperandType}", infixExpr);
                        return "unknown";
                    }
                    resultType = rightOperandType;
                }
                else
                {
                    // Неизвестный оператор
                    ReportError($"Unknown infix operator '{op}'", infixExpr);
                    return "unknown";
                }
            }

            return resultType;
        }

        private string GetPrefixExpressionType(JavaGrammarParser.PrefixExpressionContext prefixExpr)
        {
            if (prefixExpr == null) return "unknown";

            var prefixOp = prefixExpr.prefixOp();
            var innerExpr2 = prefixExpr.expression2(); // Это внутреннее выражение

            if (prefixOp != null && innerExpr2 != null)
            {
                string op = prefixOp.GetText();
                // ВАЖНО: Вызываем GetExpression2Type для внутреннего выражения
                // Это может привести к рекурсии, но она должна завершиться
                string operandType = GetExpression2Type(innerExpr2);

                // Логическое отрицание
                if (op == "!")
                {
                    if (operandType == "boolean")
                    {
                        return "boolean";
                    }
                    else
                    {
                        ReportError($"Operator '!' requires boolean operand, found: {operandType}", prefixExpr);
                        return "unknown";
                    }
                }
                // Унарные арифметические операции
                else if (op == "+" || op == "-")
                {
                    if (IsNumericType(operandType))
                    {
                        return operandType; // Тип результата совпадает с типом операнда
                    }
                    else
                    {
                        ReportError($"Unary operator '{op}' requires numeric operand, found: {operandType}", prefixExpr);
                        return "unknown";
                    }
                }
            }
            else if (prefixOp != null && innerExpr2 == null)
            {
                // Это может быть ошибка в грамматике или дереве разбора
                ReportError("Prefix operator without operand", prefixExpr);
            }

            return "unknown";
        }

        private string GetPostfixExpressionType(JavaGrammarParser.PostfixExpressionContext postfixExpr)
        {
            if (postfixExpr == null) return "unknown";

            var primary = postfixExpr.primary();
            if (primary != null)
            {
                // Определяем тип первичного выражения
                string primaryType = "unknown";

                if (primary is JavaGrammarParser.LiteralPrimaryContext literalPrimary)
                {
                    var literal = literalPrimary.literal();
                    if (literal != null)
                    {
                        if (literal.INTEGER_LITERAL() != null) primaryType = "int";
                        else if (literal.FLOATING_POINT_LITERAL() != null) primaryType = "double";
                        else if (literal.STRING_LITERAL() != null) primaryType = "String";
                        else if (literal.TRUE() != null || literal.FALSE() != null) primaryType = "boolean";
                        else if (literal.CHARACTER_LITERAL() != null) primaryType = "char";
                        else if (literal.NULL() != null) primaryType = "null";
                    }
                }
                else if (primary is JavaGrammarParser.IdentifierPrimaryContext idPrimary)
                {
                    if (idPrimary.identifier().Length > 0)
                    {
                        string varName = idPrimary.identifier()[0].GetText();
                        var symbol = _symbolTable.GetSymbol(varName);
                        primaryType = symbol?.Type ?? "unknown";
                    }
                }
                else if (primary is JavaGrammarParser.NewCreatorPrimaryContext newCreatorPrimary)
                {
                    var creator = newCreatorPrimary.creator();
                    if (creator != null && creator.createdName() != null)
                    {
                        var createdName = creator.createdName();
                        if (createdName.identifier().Length > 0)
                        {
                            primaryType = createdName.identifier()[0].GetText();
                        }
                    }
                }
                else if (primary is JavaGrammarParser.ParenthesizedPrimaryContext parenthPrimary)
                {
                    var parExpr = parenthPrimary.parExpression();
                    if (parExpr != null)
                    {
                        var innerExpr = parExpr.expression(); // Это x < y
                        if (innerExpr != null)
                        {
                            primaryType = GetExpressionType(innerExpr); // Рекурсивный вызов для анализа x < y
                        }
                    }
                }

                // Теперь проверяем postfixOp (инкремент/декремент)
                var postfixOp = postfixExpr.postfixOp();
                if (postfixOp != null)
                {
                    // Для инкремента/декремента результат - это тип переменной
                    if (postfixOp.INC() != null || postfixOp.DEC() != null)
                    {
                        if (IsNumericType(primaryType))
                        {
                            return primaryType; // Тип результата инкремента/декремента
                        }
                        else
                        {
                            ReportError($"Increment/decrement operators require numeric operand, found: {primaryType}", postfixExpr);
                            return "unknown";
                        }
                    }
                }
                // Проверяем selector (например, вызовы методов, доступ к полям, доступ к массивам)
                if (postfixExpr.selector() != null && postfixExpr.selector().Length > 0)
                {
                    // Пока возвращаем тип первичного выражения, хотя в реальности он может измениться
                    // в зависимости от вызываемого метода/поля
                    // Для простых случаев (без вызова методов) возвращаем тип primary
                    return primaryType;
                }

                // --- КЛЮЧЕВОЕ ИЗМЕНЕНИЕ: Возвращаем тип первичного выражения ---
                return primaryType;
            }

            return "unknown";
        }

        private string GetArithmeticResultType(string leftType, string rightType)
        {
            if (leftType == "double" || rightType == "double")
                return "double";
            else if (leftType == "float" || rightType == "float")
                return "float";
            else if (leftType == "long" || rightType == "long")
                return "long";
            else if (IsNumericType(leftType) && IsNumericType(rightType))
                return "int";
            else
                return "unknown";
        }


        private bool IsNumericType(string type)
        {
            if (type == null) return false;
            type = type.ToLower();
            return type == "int" || type == "double" || type == "float" || type == "long" || type == "short" || type == "byte";
        }

        private bool AreTypesCompatible(string type1, string type2)
        {
            if (type1 == null || type2 == null) return false;
            if (type1 == type2) return true;

            // Проверка числовых типов
            if (IsNumericType(type1) && IsNumericType(type2))
            {
                // Позволяем приведение от меньшего к большему
                var numericOrder = new[] { "byte", "short", "int", "long", "float", "double" };
                int idx1 = Array.IndexOf(numericOrder, type1.ToLower());
                int idx2 = Array.IndexOf(numericOrder, type2.ToLower());
                return idx1 >= 0 && idx2 >= 0 && Math.Max(idx1, idx2) <= Math.Max(idx1, idx2);
            }

            // Проверка для коллекций
            return IsCollectionType(type1) && IsCollectionType(type2);
        }

        private bool IsValidSwitchType(string type)
        {
            if (type == null) return false;
            type = type.ToLower();
            return type == "int" || type == "char" || type == "string";
        }

        private void ReportError(string message, ParserRuleContext context)
        {
            int line = context.Start.Line;
            int column = context.Start.Column;
            _errors.Add(new SemanticException(message, line, column));
        }

        private T GetParentOfType<T>(ParserRuleContext context) where T : ParserRuleContext
        {
            while (context != null)
            {
                if (context is T result)
                {
                    return result;
                }
                context = context.Parent as ParserRuleContext;
            }
            return null;
        }
        
        // Анализ классов
        public override object VisitClassDeclaration(JavaGrammarParser.ClassDeclarationContext context)
        {
            string className = context.identifier().GetText();

            // Проверяем, что в программе только один класс
            if (_symbolTable.IsDeclaredInCurrentScope("program_class"))
            {
                ReportError("Only one class is allowed in the program", context);
            }
            else
            {
                _symbolTable.Declare("program_class", "class");
            }

            // Добавляем класс в глобальную область видимости
            _symbolTable.Declare(className, "class");

            // Проверяем модификаторы класса
            bool hasPublicModifier = false;

            foreach (var modifierContext in context.modifier())
            {
                string modifierText = modifierContext.GetText();
                if (modifierText == "public")
                {
                    hasPublicModifier = true;
                }
                else
                {
                    // Разрешаем только public модификатор для соответствия требованиям
                    ReportError($"Class modifier '{modifierText}' is not supported. Only 'public' modifier is allowed.", context);
                }
            }

            // НЕ проверяем наличие метода main здесь.
            // Это делает VisitVoidMethodDeclaratorRest или другой метод, специально предназначенный для этой проверки.

            // Входим в область видимости класса
            _symbolTable.EnterScope(className);

            // Посещаем тело класса для дальнейшего анализа
            var result = base.VisitClassDeclaration(context);

            // Выходим из области видимости класса
            _symbolTable.ExitScope();

            return result;
        }

        // Анализ метода main
        public override object VisitVoidMethodDeclaratorRest(JavaGrammarParser.VoidMethodDeclaratorRestContext context)
        {
            // Проверяем, является ли это main методом
            var memberDeclContext = GetParentOfType<JavaGrammarParser.VoidMethodMemberContext>(context);
            if (memberDeclContext != null && memberDeclContext.identifier() != null)
            {
                string methodName = memberDeclContext.identifier().GetText();

                if (methodName == "main")
                {
                    // Проверяем параметры (должны быть String[] args)
                    var formalParams = context.formalParameters();
                    if (formalParams != null)
                    {
                        var formalParamDecls = formalParams.formalParameterDecls();
                        if (formalParamDecls != null)
                        {
                            // --- ИСПРАВЛЕНИЕ ---
                            // Получаем базовый тип параметра (например, "String")
                            string baseParamType = GetTypeName(formalParamDecls.type());

                            // Проверяем, является ли базовый тип "String"
                            bool isStringType = baseParamType.Trim().Equals("String", StringComparison.OrdinalIgnoreCase) ||
                                               baseParamType.Trim().Equals("java.lang.String", StringComparison.OrdinalIgnoreCase);

                            // Проверяем, что параметр является массивом (имеет размерности [])
                            // LBRACK/RBRACK находятся в том же контексте, что и базовый тип (ReferenceTypeTypeContext или BasicTypeTypeContext)
                            // Проверим, есть ли LBRACK в formalParamDecls.type()
                            bool isArray = false;
                            var typeCtx = formalParamDecls.type();
                            if (typeCtx is JavaGrammarParser.ReferenceTypeTypeContext refTypeCtx)
                            {
                                // LBRACK может быть в ReferenceTypeTypeContext
                                isArray = refTypeCtx.LBRACK() != null && refTypeCtx.LBRACK().Length > 0;
                            }
                            // Если typeCtx - BasicTypeTypeContext, проверка LBRACK там невозможна, так как BasicType не может быть массивом напрямую.
                            // else if (typeCtx is JavaGrammarParser.BasicTypeTypeContext basicTypeCtx)
                            // {
                            //     isArray = basicTypeCtx.LBRACK() != null && basicTypeCtx.LBRACK().Length > 0;
                            //     // Это маловероятно, так как int[] не может быть параметром main.
                            // }

                            // Проверяем, что базовый тип String И массивность есть
                            if (!isStringType || !isArray)
                            {
                                ReportError("Main method parameter must be of type String[]", context);
                            }
                            // --- КОНЕЦ ИСПРАВЛЕНИЯ ---

                            // Проверяем имя параметра (необязательно для семантики, но можно проверить стилю)
                            var paramRest = formalParamDecls.formalParameterDeclsRest();
                            if (paramRest != null && paramRest.variableDeclaratorId() != null)
                            {
                                string paramName = paramRest.variableDeclaratorId().identifier().GetText();
                                // Например, можно проверить, что имя не начинается с заглавной буквы
                                if (char.IsUpper(paramName.FirstOrDefault()))
                                {
                                    ReportWarning($"Parameter name '{paramName}' should start with lowercase letter", paramRest.variableDeclaratorId());
                                }
                            }
                        }
                        else
                        {
                            // formalParamDecls == null -> значит, параметров нет (public static void main())
                            ReportError("Main method must have exactly one parameter of type String[]", context);
                        }
                    }
                    else
                    {
                        // formalParameters == null -> значит, параметров нет (public static void main())
                        ReportError("Main method must have exactly one parameter of type String[]", context);
                    }

                    // Проверяем модификаторы (должны быть public static)
                    var memberClassBodyDecl = GetParentOfType<JavaGrammarParser.MemberClassBodyDeclarationContext>(context);
                    if (memberClassBodyDecl != null)
                    {
                        bool hasPublic = false, hasStatic = false;
                        foreach (var modifier in memberClassBodyDecl.modifier())
                        {
                            string modText = modifier.GetText();
                            if (modText == "public") hasPublic = true;
                            if (modText == "static") hasStatic = true;
                        }

                        if (!hasPublic || !hasStatic)
                        {
                            ReportError("Main method must be public static", context);
                        }
                    }
                }
            }

            // Продолжаем обход детей этого узла
            return base.VisitVoidMethodDeclaratorRest(context);
        }

        private void ReportWarning(string message, ParserRuleContext context)
        {
            int line = context.Start.Line;
            int column = context.Start.Column;
            _warnings.Add(new SemanticException(message, line, column));
        }


        // Анализ цикла for
        public override object VisitForStatement(JavaGrammarParser.ForStatementContext context)
        {
            // Создаем область видимости для переменных цикла
            string loopScopeName = $"for_loop_{context.Start.Line}_{context.Start.Column}";
            _symbolTable.EnterScope(loopScopeName);

            try
            {
                // Анализируем управление циклом
                if (context.forControl() != null)
                {
                    // Явно проверяем тип forControl и вызываем соответствующий метод
                    if (context.forControl() is JavaGrammarParser.EnhancedForControlContext enhancedForCtrl)
                    {
                        VisitEnhancedForControl(enhancedForCtrl);
                    }
                    else if (context.forControl() is JavaGrammarParser.TraditionalForControlContext traditionalForCtrl)
                    {
                        VisitTraditionalForControl(traditionalForCtrl);
                    }
                    else
                    {
                        // Неизвестный тип цикла
                        ReportError("Unknown for loop control structure", context.forControl());
                    }
                }

                // Анализируем тело цикла
                if (context.statement() != null)
                {
                    Visit(context.statement());
                }
            }
            finally
            {
                // Всегда выходим из области видимости цикла
                _symbolTable.ExitScope();
            }

            return null;
        }

        public override object VisitTraditionalForControl(JavaGrammarParser.TraditionalForControlContext context)
        {
            // Проверка инициализации
            if (context.forInit() != null)
            {
                Visit(context.forInit()); // <-- Это вызовет VisitVariableDeclarator
            }

            // Проверка условия цикла
            if (context.expression() != null)
            {
                string conditionType = GetExpressionType(context.expression());
                if (conditionType != "boolean")
                {
                    ReportError($"For loop condition must be of boolean type, found: {conditionType}", context);
                }
            }

            // Проверка выражения обновления
            if (context.forUpdate() != null)
            {
                foreach (var stmtExpr in context.forUpdate().statementExpression())
                {
                    var expression = stmtExpr.expression();
                    if (expression != null)
                    {
                        string exprType = GetExpressionType(expression);
                        if (string.IsNullOrEmpty(exprType) || exprType == "unknown")
                        {
                            ReportWarning($"Update expression has unknown type", expression);
                        }
                    }
                }
            }

            return null;
        }

        public override object VisitEnhancedForControl(JavaGrammarParser.EnhancedForControlContext context)
        {
            if (context.forVarControl() != null)
            {
                var forVarControl = context.forVarControl();

                // Тип элемента коллекции
                string elementType = forVarControl.type() != null ?
                    GetTypeName(forVarControl.type()) : "unknown";

                // Имя переменной элемента
                string elementName = forVarControl.variableDeclaratorId() != null ?
                    forVarControl.variableDeclaratorId().GetText() : "unknown";

                // Тип коллекции - ищем выражение в дочерних элементах
                JavaGrammarParser.ExpressionContext collectionExpression = null;

                for (int i = 0; i < context.ChildCount; i++)
                {
                    var child = context.GetChild(i);
                    if (child is JavaGrammarParser.ExpressionContext expr)
                    {
                        collectionExpression = expr;
                        break;
                    }
                }

                if (collectionExpression != null)
                {
                    string collectionType = GetExpressionType(collectionExpression);

                    // Проверка, что выражение является коллекцией
                    if (!IsCollectionType(collectionType))
                    {
                        ReportError($"Expression in for-each loop must be a collection type, found: {collectionType}", context);
                    }
                    else
                    {
                        // Проверка совместимости типов для коллекций с дженериками
                        string collectionElementType = GetCollectionElementType(collectionType);
                        if (!string.IsNullOrEmpty(collectionElementType) && !AreTypesCompatible(elementType, collectionElementType))
                        {
                            ReportError($"Incompatible types in for-each loop: collection element type {collectionElementType} and variable type {elementType}", context);
                        }
                    }

                    // Объявляем переменную цикла
                    _symbolTable.Declare(elementName, elementType);
                }
                else
                {
                    ReportError("Missing collection expression in for-each loop", context);
                }
            }

            return base.VisitEnhancedForControl(context);
        }

        // Анализ оператора break
        public override object VisitBreakStatement(JavaGrammarParser.BreakStatementContext context)
        {
            // Проверяем, что break используется только внутри цикла или switch
            string currentScope = _symbolTable.GetCurrentScope();
            bool isInLoopOrSwitch = currentScope.Contains("for_loop") ||
                                   currentScope.Contains("while_loop") ||
                                   currentScope.Contains("do_loop") ||
                                   currentScope.Contains("switch");

            if (!isInLoopOrSwitch)
            {
                ReportError("Break statement must be used inside a loop or switch statement", context);
            }

            // Если есть метка, проверяем ее объявление
            if (context.identifier() != null)
            {
                string labelName = context.identifier().GetText();
                if (!_symbolTable.IsDeclaredInCurrentScope(labelName))
                {
                    ReportError($"Label '{labelName}' is not declared", context);
                }
            }

            return null;
        }

        // Вспомогательные методы

        private bool IsCollectionType(string type)
        {
            if (string.IsNullOrEmpty(type)) return false;

            type = type.ToLower();
            return type.Contains("arraylist") || type.Contains("hashset") ||
                   type.Contains("hashmap") || type.Contains("list") ||
                   type.Contains("set") || type.Contains("map") ||
                   type.Contains("array") || type.EndsWith("[]");
        }

        public override object VisitMethodOrFieldSelector(JavaGrammarParser.MethodOrFieldSelectorContext context)
        {
            if (context.identifier() != null)
            {
                string methodName = context.identifier().GetText();

                if (IsCollectionMethod(methodName))
                {
                    string targetObjectName = GetTargetObjectName(context);

                    if (!string.IsNullOrEmpty(targetObjectName))
                    {
                        var symbol = _symbolTable.GetSymbol(targetObjectName);
                        if (symbol != null)
                        {
                            if (!IsCollectionType(symbol.Type))
                            {
                                ReportError($"Method '{methodName}' can only be called on collection objects", context);
                            }
                            else
                            {
                                // --- НОВЫЙ ВЫЗОВ ValidateCollectionMethodCall ---
                                // Проверяем базовую совместимость метода с типом коллекции
                                ValidateCollectionMethodCall(symbol.Type, methodName, context);

                                // Затем проверяем аргументы метода коллекции
                                ValidateCollectionMethodCallWithArguments(symbol.Type, methodName, context);
                                // ---
                            }
                        }
                        else
                        {
                            ReportWarning($"Target object '{targetObjectName}' not found in symbol table. Collection method check skipped.", context);
                        }
                    }
                    else
                    {
                        ReportWarning($"Could not determine target object for method call '{methodName}'. Collection method checks may be incomplete.", context);
                    }
                }
            }

            return base.VisitMethodOrFieldSelector(context);
        }

        // Вспомогательный метод для получения имени объекта, на котором вызывается метод
        private string GetTargetObjectName(JavaGrammarParser.MethodOrFieldSelectorContext context)
        {
            // Ищем родительский контекст, который может содержать информацию о целевом объекте
            var current = context.Parent as ParserRuleContext;

            // Пройдемся по нескольким уровням вверх
            for (int level = 0; level < 5 && current != null; level++)
            {
                // В выражениях вида obj.method() obj будет в родительском контексте
                if (current is JavaGrammarParser.PostfixExpressionContext postfixExpr)
                {
                    var primary = postfixExpr.primary();
                    if (primary is JavaGrammarParser.IdentifierPrimaryContext identifierPrimary)
                    {
                        if (identifierPrimary.identifier().Length > 0)
                        {
                            return identifierPrimary.identifier()[0].GetText();
                        }
                    }
                }

                // Проверим другие контексты, которые могут содержать идентификаторы
                if (current is JavaGrammarParser.PrimaryExpressionContext primaryExpr)
                {
                    var expr1 = primaryExpr.expression1();
                    if (expr1 is JavaGrammarParser.SimpleExpression2Context simpleExpr2)
                    {
                        var expr2 = simpleExpr2.expression2();
                        if (expr2 is JavaGrammarParser.PostfixExpressionContext nestedPostfixExpr)
                        {
                            var nestedPrimary = nestedPostfixExpr.primary();
                            if (nestedPrimary is JavaGrammarParser.IdentifierPrimaryContext nestedIdPrimary)
                            {
                                if (nestedIdPrimary.identifier().Length > 0)
                                {
                                    return nestedIdPrimary.identifier()[0].GetText();
                                }
                            }
                        }
                    }
                }

                current = current.Parent as ParserRuleContext;
            }

            return null;
        }

        private void ValidateCollectionMethodCall(string collectionType, string methodName, ParserRuleContext context)
        {
            // Проверяем, что метод применим к типу коллекции
            collectionType = collectionType.ToLower();

            if (collectionType.Contains("arraylist") || collectionType.Contains("list"))
            {
                if (new[] { "put", "getOrDefault", "replace" }.Contains(methodName))
                {
                    ReportError($"Method '{methodName}' is not available for List collections", context);
                }
            }
            else if (collectionType.Contains("hashmap") || collectionType.Contains("map"))
            {
                if (new[] { "add", "addAll", "get", "set" }.Contains(methodName))
                {
                    ReportError($"Method '{methodName}' signature differs for Map collections", context);
                }
            }
            else if (collectionType.Contains("hashset") || collectionType.Contains("set"))
            {
                if (new[] { "get", "put", "getOrDefault", "replace" }.Contains(methodName))
                {
                    ReportError($"Method '{methodName}' is not available for Set collections", context);
                }
            }
        }

        private bool IsCollectionMethod(string methodName)
        {
            var collectionMethods = new HashSet<string>
            {
                "add", "addAll", "remove", "removeAll", "retainAll", "contains", "containsAll",
                "size", "isEmpty", "clear", "get", "set", "put", "putAll", "remove", "replace",
                "entrySet", "keySet", "values", "containsKey", "containsValue", "getOrDefault"
            };

            return collectionMethods.Contains(methodName);
        }

        private bool IsStandardType(string typeName)
        {
            var standardTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "String", "Integer", "Double", "Boolean", "Character", "Byte", "Short", "Long", "Float", "Void"
            };

            return standardTypes.Contains(typeName);
        }

        // Проверка дженериков для коллекций

        public override object VisitTypeArguments(JavaGrammarParser.TypeArgumentsContext context)
        {
            var refTypeList = context.referenceTypeList();
            if (refTypeList != null)
            {
                foreach (var refType in refTypeList.referenceType())
                {
                    if (refType.identifier().Length > 0)
                    {
                        string typeName = refType.identifier()[0].GetText();
                        if (!_symbolTable.IsClass(typeName) && !IsStandardType(typeName))
                        {
                            ReportError($"Type '{typeName}' is not declared", refType);
                        }
                    }
                }
            }

            return base.VisitTypeArguments(context);
        }

        // Проверка индексов массивов
        public override object VisitArrayAccessSuffix(JavaGrammarParser.ArrayAccessSuffixContext context)
        {
            if (context.expression() != null)
            {
                string indexType = GetExpressionType(context.expression());
                if (!IsNumericType(indexType) && indexType != "int")
                {
                    ReportError($"Array index must be of integer type, found: {indexType}", context);
                }
            }

            return base.VisitArrayAccessSuffix(context);
        }

        public override object VisitReturnStatement(JavaGrammarParser.ReturnStatementContext context)
        {
            // Проверяем, что return находится внутри метода
            string currentScope = _symbolTable.GetCurrentScope();

            if (context.expression() != null)
            {
                string returnType = GetExpressionType(context.expression());

                // Получаем ожидаемый тип возврата метода из таблицы символов или из контекста
                string expectedReturnType = GetExpectedMethodReturnType(currentScope);

                if (!string.IsNullOrEmpty(expectedReturnType) && expectedReturnType != "void")
                {
                    if (!AreTypesCompatible(returnType, expectedReturnType))
                    {
                        ReportError($"Incompatible return type: {returnType} cannot be returned from method expecting {expectedReturnType}", context);
                    }
                }
                else if (expectedReturnType == "void")
                {
                    ReportError("Cannot return a value from a void method", context);
                }
            }
            else
            {
                // Нет выражения - возвращается void
                string expectedReturnType = GetExpectedMethodReturnType(currentScope);
                if (!string.IsNullOrEmpty(expectedReturnType) && expectedReturnType != "void")
                {
                    ReportError($"Missing return value for method returning {expectedReturnType}", context);
                }
            }

            return base.VisitReturnStatement(context);
        }

        // Вспомогательный метод для получения ожидаемого типа возврата
        private string GetExpectedMethodReturnType(string currentScope)
        {
            // В реальной реализации нужно хранить информацию о методах
            // Упрощенная версия - предполагаем, что в scope есть информация о методе
            if (currentScope.Contains("main_method"))
            {
                return "void"; // main метод всегда void
            }
            // Здесь должна быть логика для получения типа возврата из объявления метода
            return "unknown";
        }

        // Вспомогательный метод для получения типа элемента коллекции
        private string GetCollectionElementType(string collectionType)
        {
            // Реализуйте извлечение типа элемента из строки типа, например "ArrayList<String>" -> "String"
            // Это может быть сложнее, если тип не содержит дженериков (raw type)
            if (collectionType.Contains("<"))
            {
                int start = collectionType.IndexOf('<') + 1;
                int end = collectionType.Contains(",") ? collectionType.IndexOf(',') : collectionType.LastIndexOf('>');
                if (start < end)
                {
                    return collectionType.Substring(start, end - start).Trim();
                }
            }
            return "Object"; // Значение по умолчанию для raw types
        }

        // Вспомогательный метод для проверки аргументов вызова метода коллекции
        // Этот метод нужно реализовать или убедиться, что он реализован корректно
        private void ValidateCollectionMethodCallWithArguments(string collectionType, string methodName, JavaGrammarParser.MethodOrFieldSelectorContext context)
        {
            // 1. Извлечь аргументы метода из контекста
            var arguments = GetMethodArguments(context); // Вам нужно реализовать этот вспомогательный метод

            // 2. Проверить количество аргументов в зависимости от метода
            switch (methodName)
            {
                case "add":
                    if (arguments.Count == 1)
                    {
                        // Проверить тип аргумента против типа элемента коллекции
                        string collectionElementType = GetCollectionElementType(collectionType); // Реализовать
                        string argumentType = GetExpressionType(arguments[0]); // Используем существующий метод

                        if (!AreTypesCompatible(argumentType, collectionElementType))
                        {
                            ReportError($"Cannot add element of type {argumentType} to collection of type {collectionType}<element: {collectionElementType}>", context);
                        }
                    }
                    else
                    {
                        ReportError($"Method 'add' expects 1 argument", context);
                    }
                    break;

                case "put":
                    if (arguments.Count == 2)
                    {
                        // Проверить типы аргументов против типа ключа и значения коллекции (Map)
                        string collectionKeyType = GetMapKeyType(collectionType); // Реализовать
                        string collectionValueType = GetMapValueType(collectionType); // Реализовать
                        string keyType = GetExpressionType(arguments[0]);
                        string valueType = GetExpressionType(arguments[1]);

                        if (!AreTypesCompatible(keyType, collectionKeyType))
                        {
                            ReportError($"Cannot put key of type {keyType} in map of type {collectionType}<key: {collectionKeyType}, value: {collectionValueType}>", context);
                        }

                        if (!AreTypesCompatible(valueType, collectionValueType))
                        {
                            ReportError($"Cannot put value of type {valueType} in map of type {collectionType}<key: {collectionKeyType}, value: {collectionValueType}>", context);
                        }
                    }
                    else
                    {
                        ReportError($"Method 'put' expects 2 arguments", context);
                    }
                    break;

                case "remove":
                    // Аналогично, проверить тип аргумента
                    if (arguments.Count == 1)
                    {
                        string collectionElementType = GetCollectionElementType(collectionType);
                        string argumentType = GetExpressionType(arguments[0]);
                        // В Map remove может принимать Key или Key и Value
                        if (collectionType.Contains("Map"))
                        {
                            string collectionKeyType = GetMapKeyType(collectionType);
                            if (!AreTypesCompatible(argumentType, collectionKeyType))
                            {
                                ReportError($"Cannot remove key of type {argumentType} from map of type {collectionType}", context);
                            }
                        }
                        else
                        {
                            if (!AreTypesCompatible(argumentType, collectionElementType))
                            {
                                ReportError($"Cannot remove element of type {argumentType} from collection of type {collectionType}", context);
                            }
                        }
                    }
                    break;

                // Добавьте другие методы по необходимости

                default:
                    // Для других методов коллекций можно реализовать аналогичные проверки
                    break;
            }
        }

        // Вспомогательный метод для получения аргументов метода
        private List<JavaGrammarParser.ExpressionContext> GetMethodArguments(JavaGrammarParser.MethodOrFieldSelectorContext context)
        {
            var arguments = new List<JavaGrammarParser.ExpressionContext>();

            // Ищем аргументы в родительской цепочке
            var current = context.Parent as ParserRuleContext;
            while (current != null)
            {
                if (current is JavaGrammarParser.ArgumentsSuffixContext argsSuffix)
                {
                    var args = argsSuffix.arguments();
                    if (args != null && args.expressionList() != null)
                    {
                        var exprList = args.expressionList().expression();
                        if (exprList != null)
                        {
                            arguments.AddRange(exprList);
                        }
                    }
                }
                current = current.Parent as ParserRuleContext;
            }

            return arguments;
        }

        // Вспомогательные методы для получения типов из Map
        private string GetMapKeyType(string mapType)
        {
            if (mapType.Contains("<") && mapType.Contains(","))
            {
                int start = mapType.IndexOf('<') + 1;
                int end = mapType.IndexOf(',');
                if (start < end)
                {
                    return mapType.Substring(start, end - start).Trim();
                }
            }
            return "Object";
        }

        private string GetMapValueType(string mapType)
        {
            if (mapType.Contains(",") && mapType.Contains(">"))
            {
                int start = mapType.IndexOf(',') + 1;
                int end = mapType.LastIndexOf('>');
                if (start < end)
                {
                    return mapType.Substring(start, end - start).Trim();
                }
            }
            return "Object";
        }
    }
}
