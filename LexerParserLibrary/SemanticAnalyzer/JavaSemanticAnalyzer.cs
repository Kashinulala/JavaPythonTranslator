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
            if (string.IsNullOrEmpty(type1) || string.IsNullOrEmpty(type2))
                return false;

            // Позволяем приведение от Object к любому типу (для for-each)
            if (type1.Equals("object", StringComparison.OrdinalIgnoreCase))
                return true;

            // Проводим базовую проверку совместимости
            return type1.Equals(type2, StringComparison.OrdinalIgnoreCase) ||
                   IsNumericType(type1) && IsNumericType(type2);
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
        public override object VisitMethodDeclaratorRest(JavaGrammarParser.MethodDeclaratorRestContext context)
        {
            // Проверяем, является ли это main методом
            var parent = GetParentOfType<JavaGrammarParser.MethodOrFieldDeclContext>(context);
            if (parent != null && parent.identifier() != null)
            {
                string methodName = parent.identifier().GetText();

                if (methodName == "main")
                {
                    // Проверяем, что это void метод
                    if (parent.type() != null && !IsVoidType(parent.type()))
                    {
                        ReportError("Main method must have void return type", context);
                    }

                    // Проверяем параметры (должны быть String[] args)
                    var formalParams = context.formalParameters();
                    if (formalParams != null)
                    {
                        var formalParamDecls = formalParams.formalParameterDecls();
                        if (formalParamDecls != null)
                        {
                            // В правильной структуре грамматики параметры находятся в formalParameterDecls
                            // и включают в себя как тип, так и имя переменной

                            // Проверяем тип параметра и имя переменной в formalParameterDecls
                            if (formalParamDecls.type() != null && formalParamDecls.formalParameterDeclsRest() != null)
                            {
                                string paramType = GetTypeName(formalParamDecls.type());
                                var paramRest = formalParamDecls.formalParameterDeclsRest();
                                string paramName = paramRest.variableDeclaratorId() != null ?
                                    paramRest.variableDeclaratorId().GetText() : "";

                                // Проверяем, является ли это массив String
                                // Для main метода параметр должен быть String[]
                                if (!paramType.Contains("String") || !paramName.EndsWith("[]"))
                                {
                                    ReportError("Main method parameter must be of type String[]", context);
                                }
                            }
                            else
                            {
                                // Если тип или имя параметра не указаны, это ошибка для main метода
                                ReportError("Main method must have exactly one parameter of type String[]", context);
                            }
                        }
                        else
                        {
                            // Если formalParameterDecls отсутствует, значит параметров нет
                            ReportError("Main method must have exactly one parameter of type String[]", context);
                        }
                    }
                    else
                    {
                        // Если formalParameters отсутствует, значит параметров нет
                        ReportError("Main method must have exactly one parameter of type String[]", context);
                    }

                    // Проверяем модификаторы (должны быть public static)
                    var memberDecl = GetParentOfType<JavaGrammarParser.MemberClassBodyDeclarationContext>(context);
                    if (memberDecl != null)
                    {
                        bool hasPublic = false, hasStatic = false;

                        foreach (var modifier in memberDecl.modifier())
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

            return base.VisitMethodDeclaratorRest(context);
        }

        // Вспомогательный метод для проверки void типа
        private bool IsVoidType(JavaGrammarParser.TypeContext typeContext)
        {
            // Проверяем, является ли тип void
            // В сгенерированной грамматике void представлен как BasicTypeType -> BasicType
            if (typeContext is JavaGrammarParser.BasicTypeTypeContext basicTypeCtx)
            {
                var basicType = basicTypeCtx.basicType();
                if (basicType != null)
                {
                    // В сгенерированной грамматике VOID представлен как метод VOID()
                    // Проверяем, является ли текст "void"
                    string typeText = basicType.GetText().Trim();
                    if (typeText.Equals("void", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            // Проверяем, может быть это ReferenceType с именем void
            else if (typeContext is JavaGrammarParser.ReferenceTypeTypeContext refTypeCtx)
            {
                var refType = refTypeCtx.referenceType();
                if (refType != null && refType.identifier().Length > 0)
                {
                    string typeName = refType.identifier()[0].GetText();
                    return typeName.Equals("void", StringComparison.OrdinalIgnoreCase);
                }
            }

            return false;
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
                    Visit(context.forControl());
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
                Visit(context.forInit());
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
                    // statementExpression может содержать выражение присваивания, вызов метода и т.д.
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

            return base.VisitTraditionalForControl(context);
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

        // Вспомогательный метод для анализа цикла for-each

        private void VisitForEachLoop(JavaGrammarParser.EnhancedForControlContext context)
        {
            var forVarControl = context.forVarControl();
            if (forVarControl != null && forVarControl.type() != null)
            {
                string elementType = GetTypeName(forVarControl.type());
                if (!string.IsNullOrEmpty(elementType))
                {
                    // Для EnhancedForControlContext выражение коллекции доступно как дочерний элемент
                    // ищем его среди дочерних элементов контекста
                    JavaGrammarParser.ExpressionContext collectionExpression = null;

                    // Проходим по дочерним элементам, чтобы найти выражение коллекции
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
                        if (!IsCollectionType(collectionType))
                        {
                            ReportError($"Expression in for-each loop must be a collection type, found: {collectionType}", context);
                        }
                        else if (!AreTypesCompatible(collectionType, elementType))
                        {
                            ReportError($"Incompatible types in for-each loop: collection type {collectionType} and element type {elementType}", context);
                        }
                    }
                    else
                    {
                        ReportError("Missing collection expression in for-each loop", context);
                    }
                }
            }
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
                                // Проверяем аргументы метода коллекции
                                ValidateCollectionMethodCallWithArguments(symbol.Type, methodName, context);
                            }
                        }
                        else
                        {
                            ReportWarning($"Target object '{targetObjectName}' not found in symbol table. Collection method check skipped.", context);
                        }
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
            // Упрощенная реализация - в реальной системе нужно извлекать из дженериков
            if (collectionType.Contains("<"))
            {
                int start = collectionType.IndexOf('<') + 1;
                int end = collectionType.LastIndexOf('>');
                if (start < end)
                {
                    return collectionType.Substring(start, end - start);
                }
            }
            return "Object"; // по умолчанию
        }

        // Вспомогательный метод для проверки аргументов вызова метода коллекции
        private void ValidateCollectionMethodCallWithArguments(string collectionType, string methodName, JavaGrammarParser.MethodOrFieldSelectorContext context)
        {
            // Находим аргументы метода
            var arguments = GetMethodArguments(context);

            if (methodName == "add" && arguments != null && arguments.Count > 0)
            {
                string collectionElementType = GetCollectionElementType(collectionType);
                string argumentType = GetExpressionType(arguments[0]);

                if (!AreTypesCompatible(argumentType, collectionElementType))
                {
                    ReportError($"Cannot add element of type {argumentType} to collection of type {collectionType}<element: {collectionElementType}>", context);
                }
            }
            else if (methodName == "put" && arguments != null && arguments.Count >= 2)
            {
                // Проверка для Map.put(key, value)
                string collectionKeyType = GetMapKeyType(collectionType);
                string collectionValueType = GetMapValueType(collectionType);
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
                    return mapType.Substring(start, end - start);
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
