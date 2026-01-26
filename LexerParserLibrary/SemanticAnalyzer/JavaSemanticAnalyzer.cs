using Antlr4.Runtime;

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
                var localVarDecl = GetParentOfType<JavaGrammarParser.LocalVariableDeclarationStatementContext>(varDecls);
                if (localVarDecl != null)
                {
                    varType = GetTypeName(localVarDecl.type());
                }
            }
            // Обработка полей класса
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

        // Анализ цикла for
        public override object VisitForStatement(JavaGrammarParser.ForStatementContext context)
        {
            return base.VisitForStatement(context);
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

        private string GetExpression2Type(JavaGrammarParser.Expression2Context context)
        {
            if (context is JavaGrammarParser.PostfixExpressionContext postfixExpr)
            {
                var primary = postfixExpr.primary();
                if (primary is JavaGrammarParser.LiteralPrimaryContext literalPrimary)
                {
                    var literal = literalPrimary.literal();
                    if (literal.INTEGER_LITERAL() != null) return "int";
                    if (literal.FLOATING_POINT_LITERAL() != null) return "double";
                    if (literal.STRING_LITERAL() != null) return "String";
                    if (literal.TRUE() != null || literal.FALSE() != null) return "boolean";
                }
                else if (primary is JavaGrammarParser.IdentifierPrimaryContext idPrimary &&
                         idPrimary.identifier().Length > 0)
                {
                    string varName = idPrimary.identifier()[0].GetText();
                    var symbol = _symbolTable.GetSymbol(varName);
                    return symbol?.Type ?? "unknown";
                }
            }
            return "unknown";
        }

        private string GetExpressionType(JavaGrammarParser.ExpressionContext context)
        {
            if (context is JavaGrammarParser.PrimaryExpressionContext primaryExpr)
            {
                var expr1 = primaryExpr.expression1();
                if (expr1 != null && expr1 is JavaGrammarParser.SimpleExpression2Context simpleExpr2)
                {
                    var expr2 = simpleExpr2.expression2();
                    return GetExpression2Type(expr2);
                }
            }
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

        private bool IsNumericType(string type)
        {
            if (type == null) return false;
            type = type.ToLower();
            return type == "int" || type == "double" || type == "float" || type == "long" || type == "short" || type == "byte";
        }

        private bool AreTypesCompatible(string type1, string type2)
        {
            if (type1 == null || type2 == null) return false;
            return type1 == type2 || (IsNumericType(type1) && IsNumericType(type2));
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

        // Анализ метода main
        // Анализ метода main
        public override object VisitMethodDeclaratorRest(JavaGrammarParser.MethodDeclaratorRestContext context)
        {
            var parentCtx = context.Parent as JavaGrammarParser.MethodOrFieldRestContext;
            if (parentCtx?.Parent is JavaGrammarParser.MethodOrFieldDeclContext methodDecl)
            {
                string methodName = methodDecl.identifier().GetText();

                // Проверяем, что это метод main
                if (methodName == "main")
                {
                    // Ищем MemberClassBodyDeclarationContext для получения модификаторов
                    var memberClassBodyDecl = GetParentOfType<JavaGrammarParser.MemberClassBodyDeclarationContext>(methodDecl);
                    bool isPublic = false;
                    bool isStatic = false;

                    if (memberClassBodyDecl != null)
                    {
                        foreach (var modifier in memberClassBodyDecl.modifier())
                        {
                            string modifierText = modifier.GetText();
                            if (modifierText == "public") isPublic = true;
                            if (modifierText == "static") isStatic = true;
                        }
                    }

                    // Проверяем, что метод public static void
                    if (!isPublic)
                    {
                        ReportError("Method 'main' must be public", context);
                    }

                    if (!isStatic)
                    {
                        ReportError("Method 'main' must be static", context);
                    }

                    string returnType = GetTypeName(methodDecl.type());
                    if (returnType != "void")
                    {
                        ReportError("Method 'main' must have void return type", context);
                    }

                    // Проверяем параметры метода (должен быть String[] args)
                    var formalParams = context.formalParameters();
                    if (formalParams == null || formalParams.formalParameterDecls() == null)
                    {
                        ReportError("Method 'main' must have parameter 'String[] args'", context);
                    }
                    else
                    {
                        var paramDecls = formalParams.formalParameterDecls();
                        if (paramDecls.type() == null || paramDecls.formalParameterDeclsRest() == null)
                        {
                            ReportError("Method 'main' must have parameter 'String[] args'", context);
                        }
                        else
                        {
                            // Проверяем тип параметра (должен быть массив строк)
                            string paramType = GetTypeName(paramDecls.type());
                            if (paramType != "String[]")
                            {
                                ReportError("Method 'main' parameter must be of type 'String[]'", context);
                            }

                            // Проверяем имя параметра (желательно 'args')
                            string paramName = paramDecls.formalParameterDeclsRest().variableDeclaratorId().GetText();
                            if (paramName != "args")
                            {
                                ReportWarning("Method 'main' parameter name should be 'args' for standard compliance", context);
                            }
                        }
                    }

                    // Входим в область видимости метода main
                    _symbolTable.EnterScope("main");
                }
            }

            return base.VisitMethodDeclaratorRest(context);
        }

        // Анализ классов
        public override object VisitClassDeclaration(JavaGrammarParser.ClassDeclarationContext context)
        {
            string className = context.identifier().GetText();

            // Проверяем, что имя класса начинается с заглавной буквы
            if (!char.IsUpper(className[0]))
            {
                ReportError($"Class name '{className}' should start with uppercase letter", context);
            }

            // Проверяем, что в программе только один класс
            if (_symbolTable.IsDeclaredInCurrentScope("program_class"))
            {
                ReportError("Only one class is allowed in the program", context);
            }
            else
            {
                _symbolTable.Declare("program_class", "class");
            }

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

            // Проверяем наличие метода main в классе
            bool hasMainMethod = false;
            var classBody = context.classBody();
            if (classBody != null)
            {
                foreach (var declaration in classBody.classBodyDeclaration())
                {
                    // Проверяем, является ли объявление MemberClassBodyDeclarationContext
                    if (declaration is JavaGrammarParser.MemberClassBodyDeclarationContext memberDeclContext)
                    {
                        // Теперь можно безопасно вызвать memberDecl()
                        var memberDecl = memberDeclContext.memberDecl();
                        if (memberDecl != null)
                        {
                            // Проверяем void-методы
                            if (memberDecl is JavaGrammarParser.VoidMethodMemberContext voidMethod)
                            {
                                if (voidMethod.identifier().GetText() == "main")
                                {
                                    hasMainMethod = true;
                                }
                            }
                            // Проверяем методы с возвращаемым типом
                            else if (memberDecl is JavaGrammarParser.FieldOrMethodMemberContext fieldOrMethod)
                            {
                                if (fieldOrMethod.methodOrFieldDecl() != null &&
                                    fieldOrMethod.methodOrFieldDecl().identifier().GetText() == "main")
                                {
                                    hasMainMethod = true;
                                }
                            }
                        }
                    }
                }
            }

            if (!hasMainMethod)
            {
                ReportError("Class must contain a 'main' method", context);
            }

            // Входим в область видимости класса
            _symbolTable.EnterScope(className);

            // Посещаем тело класса для дальнейшего анализа
            var result = base.VisitClassDeclaration(context);

            // Выходим из области видимости класса
            _symbolTable.ExitScope();

            return result;
        }

        private void ReportWarning(string message, ParserRuleContext context)
        {
            int line = context.Start.Line;
            int column = context.Start.Column;
            _warnings.Add(new SemanticException(message, line, column));
        }
    }
}
