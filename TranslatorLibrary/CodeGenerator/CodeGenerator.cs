using System.Text;
using Antlr4.Runtime.Tree;
using TranslatorLibrary.SemanticAnalyzer;

namespace TranslatorLibrary.CodeGenerator
{
    public class JavaCodeGenerator : JavaGrammarBaseVisitor<StringBuilder>
    {
        private readonly StringBuilder _output = new StringBuilder();
        private readonly SymbolTable _symbolTable;
        private int _indentLevel = 0;
        private readonly string _indentString = "    ";

        // Контекстные флаги
        private bool _inMethod = false;
        private string _currentMethodName = "";
        private bool _inLoop = false;
        private bool _inSwitch = false;
        private readonly Stack<string> _loopStack = new Stack<string>();

        // Счетчик временных переменных
        private int _tempCounter = 0;

        public JavaCodeGenerator(SymbolTable symbolTable)
        {
            _symbolTable = symbolTable;
        }

        public string GetGeneratedCode() => _output.ToString();

        public void Generate(IParseTree tree)
        {
            _output.Clear();
            Visit(tree);
            PostProcessCode();
        }

        #region Вспомогательные методы

        private void Append(string text) => _output.Append(text);
        private void AppendLine(string text) => _output.AppendLine(text);

        private void AppendIndent()
        {
            for (int i = 0; i < _indentLevel; i++)
                _output.Append(_indentString);
        }

        private void IncreaseIndent() => _indentLevel++;
        private void DecreaseIndent() => _indentLevel = Math.Max(0, _indentLevel - 1);

        private string GenerateTempName(string prefix = "temp") => $"{prefix}_{_tempCounter++}";

        private string ConvertJavaTypeToPython(string javaType)
        {
            if (string.IsNullOrEmpty(javaType)) return "";

            // Обработка массивов
            if (javaType.EndsWith("[]"))
                return "list";

            return javaType.ToLower() switch
            {
                "int" or "short" or "byte" or "long" => "int",
                "float" or "double" => "float",
                "boolean" => "bool",
                "char" => "str", // Один символ
                "string" => "str",
                _ => javaType
            };
        }

        private string GetDefaultValueForType(string javaType)
        {
            if (string.IsNullOrEmpty(javaType)) return "None";

            if (javaType.EndsWith("[]"))
                return "[]";

            return javaType.ToLower() switch
            {
                "int" or "short" or "byte" or "long" => "0",
                "float" or "double" => "0.0",
                "boolean" => "False",
                "char" => "''",
                "string" => "\"\"",
                _ => "None"
            };
        }

        private bool IsIntegerType(string type)
        {
            type = type?.ToLower() ?? "";
            return type == "int" || type == "short" || type == "byte" || type == "long";
        }

        private void PostProcessCode()
        {
            // Заменяем System.out.println и System.out.print на print
            string code = _output.ToString();

            // Заменяем System.out.println(...) на print(...)
            code = System.Text.RegularExpressions.Regex.Replace(
                code,
                @"System\.out\.println\((.*?)\)",
                match => $"print({match.Groups[1].Value})");

            // Заменяем System.out.print(...) на print(..., end='')
            code = System.Text.RegularExpressions.Regex.Replace(
                code,
                @"System\.out\.print\((.*?)\)",
                match => $"print({match.Groups[1].Value}, end='')");

            // Исправляем конкатенацию строк: "строка" + число/переменная -> "строка" + str(число/переменная)
            // Это простое исправление, которое работает для большинства случаев
            code = FixStringConcatenation(code);

            _output.Clear();
            _output.Append(code);

            // Добавляем вызов main, если он есть
            if (code.Contains("def main(") && !code.Contains("if __name__ =="))
            {
                _output.AppendLine("");
                _output.AppendLine("if __name__ == \"__main__\":");
                _output.Append("    ");
                _output.AppendLine("main()");
            }
        }

        private string FixStringConcatenation(string code)
        {
            // Паттерн для поиска конкатенации строки с чем-либо: "строка" + что-то
            // Группа 1: строка в кавычках (двойных или одинарных)
            // Группа 2: выражение после +
            string pattern = @"(""[^""]*""|'[^']*')\s*\+\s*([^,\n;\)]+)";

            // Заменяем все найденные конкатенации
            return System.Text.RegularExpressions.Regex.Replace(
                code,
                pattern,
                match =>
                {
                    string stringLiteral = match.Groups[1].Value;
                    string expression = match.Groups[2].Value.Trim();

                    // Убираем возможные скобки вокруг выражения
                    expression = expression.TrimStart('(').TrimEnd(')');

                    // Оборачиваем выражение в str()
                    return $"{stringLiteral} + str({expression})";
                },
                System.Text.RegularExpressions.RegexOptions.Singleline);
        }

        #endregion

        #region Корневые элементы программы

        public override StringBuilder VisitCompilationUnit(JavaGrammarParser.CompilationUnitContext context)
        {
            // Генерация заголовка
            AppendLine("# Generated from Java to Python");
            AppendLine("# Auto-generated code");
            AppendLine("");

            // Обходим импорты (просто комментируем)
            if (context.importDeclaration() != null)
            {
                foreach (var import in context.importDeclaration())
                {
                    Append("# Import: ");
                    AppendLine(import.GetText());
                }
                AppendLine("");
            }

            // Обрабатываем объявления типов (только класс Main)
            if (context.typeDeclaration() != null)
            {
                foreach (var typeDecl in context.typeDeclaration())
                {
                    Visit(typeDecl);
                }
            }

            return _output;
        }

        public override StringBuilder VisitClassDeclaration(JavaGrammarParser.ClassDeclarationContext context)
        {
            // Не генерируем класс в Python, просто обходим его тело
            if (context.classBody() != null)
            {
                Visit(context.classBody());
            }

            return _output;
        }

        public override StringBuilder VisitClassBody(JavaGrammarParser.ClassBodyContext context)
        {
            // Обходим все объявления внутри класса
            if (context.classBodyDeclaration() != null)
            {
                foreach (var decl in context.classBodyDeclaration())
                {
                    Visit(decl);
                }
            }

            return _output;
        }

        public override StringBuilder VisitMemberClassBodyDeclaration(JavaGrammarParser.MemberClassBodyDeclarationContext context)
        {
            // Игнорируем модификаторы (public, static и т.д.)
            // Просто обходим объявление члена
            return VisitChildren(context);
        }

        #endregion

        #region Методы и функции

        public override StringBuilder VisitVoidMethodMember(JavaGrammarParser.VoidMethodMemberContext context)
        {
            string methodName = context.identifier()?.GetText() ?? "unknown_method";
            _currentMethodName = methodName;
            _inMethod = true;

            // Генерируем объявление функции Python
            Append($"def {methodName}(");

            return VisitChildren(context);
        }

        public override StringBuilder VisitVoidMethodDeclaratorRest(JavaGrammarParser.VoidMethodDeclaratorRestContext context)
        {
            // Параметры метода
            if (context.formalParameters() != null)
            {
                Visit(context.formalParameters());
            }

            AppendLine("):");

            IncreaseIndent();

            // Тело метода
            if (context.block() != null)
            {
                Visit(context.block());
            }
            else if (context.SEMI() != null)
            {
                // Абстрактный метод
                AppendIndent();
                AppendLine("pass");
            }

            DecreaseIndent();
            AppendLine("");

            _inMethod = false;
            _currentMethodName = "";

            return _output;
        }

        public override StringBuilder VisitMethodOrFieldDecl(JavaGrammarParser.MethodOrFieldDeclContext context)
        {
            // Получаем имя метода
            string methodName = context.identifier()?.GetText() ?? "unknown_method";
            _currentMethodName = methodName;
            _inMethod = true;

            // Генерируем объявление функции Python
            Append($"def {methodName}");

            return VisitChildren(context);
        }

        public override StringBuilder VisitMethodRest(JavaGrammarParser.MethodRestContext context)
        {
            // Получаем methodDeclaratorRest для доступа к формальным параметрам и телу метода
            var methodDeclarator = context.methodDeclaratorRest();

            if (methodDeclarator != null)
            {
                // Параметры метода
                if (methodDeclarator.formalParameters() != null)
                {
                    Append("(");
                    VisitFormalParameters(methodDeclarator.formalParameters());
                    Append(")");
                }
                else
                {
                    Append("()");
                }

                AppendLine("):");

                IncreaseIndent();

                // Тело метода
                if (methodDeclarator.block() != null)
                {
                    Visit(methodDeclarator.block());
                }
                else if (methodDeclarator.SEMI() != null)
                {
                    // Абстрактный метод или объявление без тела
                    AppendIndent();
                    AppendLine("pass");
                }

                DecreaseIndent();
                AppendLine("");
            }

            _inMethod = false;
            _currentMethodName = "";

            return _output;
        }

        public override StringBuilder VisitFormalParameters(JavaGrammarParser.FormalParametersContext context)
        {
            if (context.formalParameterDecls() != null)
            {
                Visit(context.formalParameterDecls());
            }
            else
            {
                Append(""); // Пустые параметры
            }

            return _output;
        }

        public override StringBuilder VisitFormalParameterDecls(JavaGrammarParser.FormalParameterDeclsContext context)
        {
            if (context.formalParameterDeclsRest() != null)
            {
                // Обрабатываем первый параметр
                if (context.formalParameterDeclsRest().variableDeclaratorId() != null)
                {
                    string paramName = context.formalParameterDeclsRest().variableDeclaratorId().GetText();
                    Append(paramName);
                }

                // Проверяем, есть ли еще параметры
                if (context.formalParameterDeclsRest().formalParameterDecls() != null)
                {
                    Append(", ");
                    Visit(context.formalParameterDeclsRest().formalParameterDecls());
                }
            }

            return _output;
        }

        #endregion

        #region Объявления переменных

        public override StringBuilder VisitVariableDeclarator(JavaGrammarParser.VariableDeclaratorContext context)
        {
            string varName = context.identifier()?.GetText() ?? "var";

            Append(varName);

            if (context.variableDeclaratorRest()?.variableInitializer() != null)
            {
                Append(" = ");
                Visit(context.variableDeclaratorRest().variableInitializer());
            }
            else
            {
                // Инициализация по умолчанию
                var symbol = _symbolTable.GetSymbol(varName);
                if (symbol != null && !string.IsNullOrEmpty(symbol.Type))
                {
                    string defaultValue = GetDefaultValueForType(symbol.Type);
                    Append($" = {defaultValue}");
                }
                else
                {
                    Append(" = None");
                }
            }

            return _output;
        }

        public override StringBuilder VisitLocalVariableDeclarationStatement(
            JavaGrammarParser.LocalVariableDeclarationStatementContext context)
        {
            //AppendIndent();
            VisitChildren(context);
            AppendLine("");
            return _output;
        }

        public override StringBuilder VisitVariableInitializer(JavaGrammarParser.VariableInitializerContext context)
        {
            if (context.expression() != null)
            {
                Visit(context.expression());
            }
            else if (context.arrayInitializer() != null)
            {
                Visit(context.arrayInitializer());
            }

            return _output;
        }

        #endregion

        #region Выражения

        public override StringBuilder VisitPrimaryExpression(JavaGrammarParser.PrimaryExpressionContext context)
        {
            if (context.expression1() != null)
            {
                Visit(context.expression1());
            }

            return _output;
        }

        public override StringBuilder VisitAssignmentExpression(JavaGrammarParser.AssignmentExpressionContext context)
        {
            var expressions = context.expression();
            //AppendIndent();
            if (expressions.Length >= 2)
            {
                // Левая часть
                Visit(expressions[0]);

                // Оператор
                string op = context.assignmentOperator()?.GetText() ?? "=";

                switch (op)
                {
                    case "+=": Append(" += "); break;
                    case "-=": Append(" -= "); break;
                    case "*=": Append(" *= "); break;
                    case "/=": Append(" /= "); break;
                    case "%=": Append(" %= "); break;
                    default: Append(" = "); break;
                }

                // Правая часть
                Visit(expressions[1]);
            }
            AppendLine("");
            return _output;
        }

        public override StringBuilder VisitInfixExpression(JavaGrammarParser.InfixExpressionContext context)
        {
            var expressions = context.expression2();
            var operators = context.infixOp();

            if (expressions != null && expressions.Length > 0)
            {
                Visit(expressions[0]);

                for (int i = 0; i < operators.Length && i + 1 < expressions.Length; i++)
                {
                    string op = operators[i].GetText();

                    // Преобразование операторов
                    switch (op)
                    {
                        case "&&": Append(" and "); break;
                        case "||": Append(" or "); break;
                        case "==": Append(" == "); break;
                        case "!=": Append(" != "); break;
                        default: Append($" {op} "); break;
                    }

                    Visit(expressions[i + 1]);
                }
            }

            return _output;
        }

        public override StringBuilder VisitSimpleExpression2(JavaGrammarParser.SimpleExpression2Context context)
        {
            if (context.expression2() != null)
            {
                Visit(context.expression2());
            }

            return _output;
        }

        public override StringBuilder VisitPrefixExpression(JavaGrammarParser.PrefixExpressionContext context)
        {
            string op = context.prefixOp()?.GetText() ?? "";

            switch (op)
            {
                case "!": Append("not "); break;
                case "+": Append("+"); break;
                case "-": Append("-"); break;
            }

            if (context.expression2() != null)
            {
                Visit(context.expression2());
            }

            return _output;
        }

        public override StringBuilder VisitPostfixExpression(JavaGrammarParser.PostfixExpressionContext context)
        {
            // Обрабатываем первичное выражение (идентификатор)
            if (context.primary() != null)
            {
                Visit(context.primary());
            }

            // Обрабатываем постфиксный оператор (++ или --)
            var postfixOp = context.postfixOp();
            if (postfixOp != null)
            {
                // Для постфиксного оператора в отдельном выражении (например, просто "i++")
                // нужно сгенерировать операцию инкремента/декремента как отдельный оператор
                if (postfixOp.INC() != null)
                {
                    AppendLine(" += 1");
                }
                else if (postfixOp.DEC() != null)
                {
                    AppendLine(" -= 1");
                }
            }

            return _output;
        }

        #endregion

        #region Первичные выражения и литералы

        public override StringBuilder VisitLiteralPrimary(JavaGrammarParser.LiteralPrimaryContext context)
        {
            if (context.literal() != null)
            {
                string literal = context.literal().GetText();

                switch (literal)
                {
                    case "true": Append("True"); break;
                    case "false": Append("False"); break;
                    case "null": Append("None"); break;
                    default:
                        Append(literal);
                        break;
                }
            }

            return _output;
        }

        public override StringBuilder VisitIdentifierPrimary(JavaGrammarParser.IdentifierPrimaryContext context)
        {
            if (context.identifier() != null && context.identifier().Length > 0)
            {
                // Первый идентификатор
                Append(context.identifier()[0].GetText());

                // Дополнительные идентификаторы через точку
                for (int i = 1; i < context.identifier().Length; i++)
                {
                    Append($".{context.identifier()[i].GetText()}");
                }

                // Суффиксы
                if (context.identifierSuffix() != null)
                {
                    Visit(context.identifierSuffix());
                }
            }

            return _output;
        }

        public override StringBuilder VisitNewCreatorPrimary(JavaGrammarParser.NewCreatorPrimaryContext context)
        {
            if (context.creator() != null)
            {
                Visit(context.creator());
            }

            return _output;
        }

        public override StringBuilder VisitParenthesizedPrimary(JavaGrammarParser.ParenthesizedPrimaryContext context)
        {
            if (context.parExpression() != null)
            {
                Append("(");
                Visit(context.parExpression());
                Append(")");
            }

            return _output;
        }

        #endregion

        #region Операторы управления

        public override StringBuilder VisitIfStatement(JavaGrammarParser.IfStatementContext context)
        {
            //AppendIndent();
            Append("if ");

            // Условие
            if (context.parExpression() != null)
            {
                Visit(context.parExpression());
            }

            AppendLine(":");

            IncreaseIndent();

            // Тело if
            if (context.statement(0) != null)
            {
                Visit(context.statement(0));
            }

            DecreaseIndent();

            // Блок else
            if (context.ELSE() != null && context.statement(1) != null)
            {
                AppendIndent();
                AppendLine("else:");

                IncreaseIndent();
                Visit(context.statement(1));
                DecreaseIndent();
            }

            return _output;
        }

        public override StringBuilder VisitWhileStatement(JavaGrammarParser.WhileStatementContext context)
        {
            string loopLabel = $"while_loop_{_loopStack.Count}";
            _loopStack.Push(loopLabel);
            _inLoop = true;

            //AppendIndent();
            Append("while ");

            // Условие
            if (context.parExpression() != null)
            {
                Visit(context.parExpression());
            }

            AppendLine(":");

            IncreaseIndent();

            // Тело цикла
            if (context.statement() != null)
            {
                Visit(context.statement());
            }

            DecreaseIndent();

            _loopStack.Pop();
            _inLoop = _loopStack.Count > 0;

            return _output;
        }

        public override StringBuilder VisitDoWhileStatement(JavaGrammarParser.DoWhileStatementContext context)
        {
            string loopLabel = $"do_while_loop_{_loopStack.Count}";
            _loopStack.Push(loopLabel);
            _inLoop = true;

            AppendIndent();
            AppendLine("while True:");

            IncreaseIndent();

            // Тело цикла
            if (context.statement() != null)
            {
                Visit(context.statement());
            }

            // Условие продолжения
            AppendIndent();
            Append("if not (");

            if (context.parExpression() != null)
            {
                Visit(context.parExpression());
            }

            AppendLine("):");

            AppendIndent();
            Append("    ");
            AppendLine("break");

            DecreaseIndent();

            _loopStack.Pop();
            _inLoop = _loopStack.Count > 0;

            return _output;
        }

        public override StringBuilder VisitForStatement(JavaGrammarParser.ForStatementContext context)
        {
            if (context.forControl() is JavaGrammarParser.TraditionalForControlContext traditionalFor)
            {
                // Традиционный for: for (int i = 0; i < 3; i++)
                // Пытаемся преобразовать в for i in range(...)
                _inLoop = true;

                string varName = "i"; // имя переменной по умолчанию
                string startValue = "0";
                string endCondition = "";
                string stepValue = "1";
                bool canConvertToRange = true;

                // 1. Извлекаем инициализацию (forInit)
                if (traditionalFor.forInit() != null)
                {
                    var forInit = traditionalFor.forInit();

                    // Вариант 1: Объявление переменной в for (int i = 0)
                    if (forInit.forDeclarationContext() != null)
                    {
                        var declContext = forInit.forDeclarationContext();
                        var varDecls = declContext.variableDeclarators();
                        if (varDecls != null && varDecls.variableDeclarator().Length > 0)
                        {
                            var firstVar = varDecls.variableDeclarator()[0];

                            // Имя переменной
                            varName = firstVar.identifier()?.GetText() ?? "i";

                            // Начальное значение
                            if (firstVar.variableDeclaratorRest()?.variableInitializer() != null)
                            {
                                //AppendIndent();
                                Append($"{varName} = ");
                                Visit(firstVar.variableDeclaratorRest().variableInitializer());
                                AppendLine("");

                                // Сохраняем начальное значение для range
                                var initializerText = firstVar.variableDeclaratorRest().variableInitializer().GetText();
                                if (!string.IsNullOrEmpty(initializerText))
                                {
                                    startValue = initializerText;
                                }
                            }
                        }
                    }
                    // Вариант 2: Присваивание без объявления типа (i = 0)
                    else if (forInit.statementExpression() != null && forInit.statementExpression().Length > 0)
                    {
                        // Присваивание без объявления типа: i = 0
                        var exprList = forInit.statementExpression();
                        if (exprList.Length > 0)
                        {
                            var firstExpr = exprList[0];
                            var expr = firstExpr.expression();

                            // Обрабатываем присваивание
                            if (expr is JavaGrammarParser.AssignmentExpressionContext assignExpr)
                            {
                                // Левая часть - имя переменной
                                if (assignExpr.expression().Length > 0)
                                {
                                    var leftExpr = assignExpr.expression()[0];
                                    if (leftExpr != null)
                                    {
                                        varName = leftExpr.GetText();

                                        AppendIndent();
                                        Append($"{varName} = ");

                                        // Правая часть - начальное значение
                                        if (assignExpr.expression().Length > 1)
                                        {
                                            Visit(assignExpr.expression()[1]);
                                            startValue = assignExpr.expression()[1].GetText();
                                        }
                                        AppendLine("");
                                    }
                                }
                            }
                            // Обрабатываем инкремент/декремент (i++)
                            else if (expr is JavaGrammarParser.PrimaryExpressionContext primExpr)
                            {
                                // Для случая for (i = 0; ...) нужно искать присваивание в primary
                                // Упрощенная обработка
                                varName = primExpr.GetText();
                                AppendIndent();
                                Append($"{varName} = 0");
                                AppendLine("");
                                startValue = "0";
                            }
                        }
                    }
                }
                else
                {
                    // Нет инициализации, цикл может использовать уже объявленную переменную
                    canConvertToRange = false;
                }

                // 2. Извлекаем условие
                if (traditionalFor.expression() != null)
                {
                    var condition = traditionalFor.expression();
                    var conditionText = condition.GetText();

                    // Пытаемся распарсить условие вида i < 10, i <= n, и т.д.
                    if (conditionText.Contains("<="))
                    {
                        var parts = conditionText.Split(new[] { "<=" }, StringSplitOptions.None);
                        if (parts.Length == 2 && parts[0].Trim() == varName)
                        {
                            endCondition = parts[1].Trim();
                            // Для range нужно +1 к конечному значению при <=
                            endCondition = $"{endCondition} + 1";
                        }
                        else
                        {
                            canConvertToRange = false;
                        }
                    }
                    else if (conditionText.Contains("<"))
                    {
                        var parts = conditionText.Split('<');
                        if (parts.Length == 2 && parts[0].Trim() == varName)
                        {
                            endCondition = parts[1].Trim();
                        }
                        else
                        {
                            canConvertToRange = false;
                        }
                    }
                    else if (conditionText.Contains(">="))
                    {
                        var parts = conditionText.Split(new[] { ">=" }, StringSplitOptions.None);
                        if (parts.Length == 2 && parts[0].Trim() == varName)
                        {
                            endCondition = parts[1].Trim();
                            // Для range с отрицательным шагом
                            stepValue = "-1";
                            startValue = endCondition;
                            endCondition = $"{startValue} - 1";
                        }
                        else
                        {
                            canConvertToRange = false;
                        }
                    }
                    else if (conditionText.Contains(">"))
                    {
                        var parts = conditionText.Split('>');
                        if (parts.Length == 2 && parts[0].Trim() == varName)
                        {
                            endCondition = parts[1].Trim();
                            stepValue = "-1";
                        }
                        else
                        {
                            canConvertToRange = false;
                        }
                    }
                    else
                    {
                        // Нестандартное условие
                        canConvertToRange = false;
                    }
                }
                else
                {
                    // Нет условия - бесконечный цикл
                    canConvertToRange = false;
                }

                // 3. Извлекаем шаг (forUpdate)
                if (traditionalFor.forUpdate() != null)
                {
                    var forUpdate = traditionalFor.forUpdate();
                    var stmtExprs = forUpdate.statementExpression(); // Уже массив

                    if (stmtExprs != null && stmtExprs.Length > 0)
                    {
                        var firstUpdate = stmtExprs[0];
                        var updateText = firstUpdate.GetText();

                        // Обрабатываем i++ или i += 1
                        if (updateText == $"{varName}++" || updateText == $"{varName} += 1")
                        {
                            stepValue = "1";
                        }
                        // Обрабатываем i--
                        else if (updateText == $"{varName}--" || updateText == $"{varName} -= 1")
                        {
                            stepValue = "-1";
                        }
                        // Обрабатываем i += n
                        else if (updateText.StartsWith($"{varName} += "))
                        {
                            stepValue = updateText.Substring($"{varName} += ".Length);
                        }
                        // Обрабатываем i -= n
                        else if (updateText.StartsWith($"{varName} -= "))
                        {
                            var step = updateText.Substring($"{varName} -= ".Length);
                            stepValue = $"-{step}";
                        }
                        else if (updateText == $"{varName}++" || updateText == $"{varName}--")
                        {
                            // Уже обработано выше
                        }
                        else
                        {
                            // Нестандартное обновление
                            canConvertToRange = false;
                        }
                    }
                }
                else
                {
                    // Нет обновления - возможно, цикл без изменения переменной
                    canConvertToRange = false;
                }

                // 4. Генерируем цикл for с range или while
                if (canConvertToRange && !string.IsNullOrEmpty(endCondition))
                {
                    // Генерируем for с range
                    AppendIndent();

                    if (stepValue == "1")
                    {
                        AppendLine($"for {varName} in range({startValue}, {endCondition}):");
                    }
                    else if (stepValue == "-1")
                    {
                        // Для обратного цикла
                        AppendLine($"for {varName} in range({startValue}, {endCondition}, -1):");
                    }
                    else
                    {
                        AppendLine($"for {varName} in range({startValue}, {endCondition}, {stepValue}):");
                    }

                    IncreaseIndent();

                    // Тело цикла
                    if (context.statement() != null)
                    {
                        Visit(context.statement());
                    }

                    DecreaseIndent();
                }
                else
                {
                    // Если не удалось распарсить в range, генерируем while как запасной вариант

                    // Инициализация уже сгенерирована выше (если была)

                    AppendIndent();
                    Append($"while ");

                    if (traditionalFor.expression() != null)
                    {
                        Visit(traditionalFor.expression());
                    }
                    else
                    {
                        Append("True");
                    }

                    AppendLine(":");

                    IncreaseIndent();

                    // Тело цикла
                    if (context.statement() != null)
                    {
                        Visit(context.statement());
                    }

                    // Обновление переменной
                    if (traditionalFor.forUpdate() != null)
                    {
                        AppendIndent();
                        Visit(traditionalFor.forUpdate());
                        AppendLine("");
                    }

                    DecreaseIndent();
                }
            }
            else if (context.forControl() is JavaGrammarParser.EnhancedForControlContext enhancedFor)
            {
                // Enhanced for (for-each): for (int item : collection)
                AppendIndent();
                Append("for ");

                if (enhancedFor.forVarControl() != null &&
                    enhancedFor.forVarControl().variableDeclaratorId() != null)
                {
                    string varName = enhancedFor.forVarControl().variableDeclaratorId().GetText();
                    Append($"{varName} in ");
                }

                // Выражение коллекции
                //if (enhancedFor.expression() != null)
                //{
                //    Visit(enhancedFor.expression());
                //}
                //else
                //{
                //    Append("collection"); // Заглушка
                //}

                AppendLine(":");

                IncreaseIndent();

                // Тело цикла
                if (context.statement() != null)
                {
                    Visit(context.statement());
                }

                DecreaseIndent();
            }
            else
            {
                // Неизвестный тип for, генерируем while
                AppendIndent();
                AppendLine("while True:");

                IncreaseIndent();

                // Тело цикла
                if (context.statement() != null)
                {
                    Visit(context.statement());
                }

                DecreaseIndent();
            }

            _inLoop = false;
            return _output;
        }

        public override StringBuilder VisitTraditionalForControl(JavaGrammarParser.TraditionalForControlContext context)
        {
            // Этот метод вызывается из VisitForStatement
            return VisitChildren(context);
        }

        public override StringBuilder VisitEnhancedForControl(JavaGrammarParser.EnhancedForControlContext context)
        {
            // Этот метод вызывается из VisitForStatement
            return VisitChildren(context);
        }

        public override StringBuilder VisitForVarControl(JavaGrammarParser.ForVarControlContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitForInit(JavaGrammarParser.ForInitContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitForUpdate(JavaGrammarParser.ForUpdateContext context)
        {
            if (context.statementExpression() != null)
            {
                foreach (var stmtExpr in context.statementExpression())
                {
                    var expr = stmtExpr.expression();
                    if (expr != null)
                    {
                        // Обрабатываем инкремент/декремент отдельно
                        if (expr is JavaGrammarParser.PrimaryExpressionContext primExpr)
                        {
                            if (primExpr.expression1() is JavaGrammarParser.SimpleExpression2Context simpleExpr2)
                            {
                                if (simpleExpr2.expression2() is JavaGrammarParser.PostfixExpressionContext postfixExpr)
                                {
                                    // Это постфиксный инкремент/декремент: i++
                                    var primary = postfixExpr.primary();
                                    if (primary is JavaGrammarParser.IdentifierPrimaryContext idPrimary)
                                    {
                                        string varName = idPrimary.identifier()[0].GetText();
                                        string op = postfixExpr.postfixOp()?.GetText() ?? "";

                                        if (op == "++")
                                        {
                                            Append($"{varName} += 1");
                                        }
                                        else if (op == "--")
                                        {
                                            Append($"{varName} -= 1");
                                        }
                                        return _output;
                                    }
                                }
                            }
                        }

                        // Общий случай
                        Visit(expr);
                    }
                }
            }

            return _output;
        }

        public override StringBuilder VisitSwitchStatement(JavaGrammarParser.SwitchStatementContext context)
        {
            _inSwitch = true;

            AppendIndent();
            AppendLine("# Switch statement converted to if-elif-else");

            if (context.parExpression()?.expression() != null)
            {
                string switchVar = GenerateTempName("switch_val");

                AppendIndent();
                Append($"{switchVar} = ");
                Visit(context.parExpression().expression());
                AppendLine("");

                if (context.switchBlockStatementGroups() != null)
                {
                    bool firstCase = true;

                    foreach (var group in context.switchBlockStatementGroups().switchBlockStatementGroup())
                    {
                        if (group.switchLabels() != null)
                        {
                            foreach (var label in group.switchLabels().switchLabel())
                            {
                                AppendIndent();

                                if (label is JavaGrammarParser.CaseExprLabelContext caseLabel)
                                {
                                    if (firstCase)
                                    {
                                        Append("if ");
                                        firstCase = false;
                                    }
                                    else
                                    {
                                        Append("elif ");
                                    }

                                    Append($"{switchVar} == ");

                                    if (caseLabel.expression() != null)
                                    {
                                        Visit(caseLabel.expression());
                                    }

                                    AppendLine(":");

                                    IncreaseIndent();

                                    // Тело case
                                    if (group.blockStatements() != null)
                                    {
                                        Visit(group.blockStatements());
                                    }

                                    DecreaseIndent();
                                }
                                else if (label is JavaGrammarParser.DefaultLabelContext)
                                {
                                    //Append("else:");
                                    AppendLine("");

                                    //IncreaseIndent();

                                    //// Тело default
                                    //if (group.blockStatements() != null)
                                    //{
                                    //    Visit(group.blockStatements());
                                    //}

                                    //DecreaseIndent();
                                }
                            }
                        }
                    }
                }
            }

            _inSwitch = false;

            return _output;
        }

        #endregion

        #region Операторы переходов

        public override StringBuilder VisitBreakStatement(JavaGrammarParser.BreakStatementContext context)
        {
            //AppendIndent();

            if (_inLoop)
            {
                AppendLine("break");
            }
            else
            {
                AppendLine("");
            }

            return _output;
        }

        public override StringBuilder VisitContinueStatement(JavaGrammarParser.ContinueStatementContext context)
        {
            //AppendIndent();

            if (_inLoop)
            {
                AppendLine("continue");
            }
            else
            {
                AppendLine("continue");
            }

            return _output;
        }

        public override StringBuilder VisitReturnStatement(JavaGrammarParser.ReturnStatementContext context)
        {
            AppendIndent();
            Append("return");

            if (context.expression() != null)
            {
                Append(" ");
                Visit(context.expression());
            }

            AppendLine("");

            return _output;
        }

        #endregion

        #region Массивы

        public override StringBuilder VisitArrayInitializer(JavaGrammarParser.ArrayInitializerContext context)
        {
            Append("[");

            if (context.variableInitializers() != null)
            {
                bool first = true;
                foreach (var initializer in context.variableInitializers().variableInitializer())
                {
                    if (!first) Append(", ");
                    first = false;
                    VisitVariableInitializer(initializer);
                }
            }

            Append("]");

            return _output;
        }

        public override StringBuilder VisitArrayAccessSuffix(JavaGrammarParser.ArrayAccessSuffixContext context)
        {
            Append("[");

            if (context.expression() != null)
            {
                Visit(context.expression());
            }

            Append("]");

            return _output;
        }

        public override StringBuilder VisitClassCreator(JavaGrammarParser.ClassCreatorContext context)
        {
            // Создание объекта
            if (context.createdName() != null)
            {
                Append(context.createdName().GetText());
            }

            if (context.classCreatorRest()?.arguments() != null)
            {
                Append("(");
                Visit(context.classCreatorRest().arguments());
                Append(")");
            }

            return _output;
        }

        public override StringBuilder VisitArrayCreatorFromBasicType(JavaGrammarParser.ArrayCreatorFromBasicTypeContext context)
        {
            // Создание массива базового типа: new int[10]
            string typeName = context.basicType()?.GetText() ?? "int";
            string defaultValue = GetDefaultValueForType(typeName);

            // Простая реализация: [default] * размер
            // В реальности нужно парсить arrayCreatorRest для получения размера
            Append($"[{defaultValue}] * 10"); // Заглушка: всегда размер 10

            return _output;
        }

        public override StringBuilder VisitArrayCreatorFromClass(JavaGrammarParser.ArrayCreatorFromClassContext context)
        {
            // Создание массива класса: new String[10]
            Append("[None] * 10"); // Заглушка: всегда размер 10

            return _output;
        }

        #endregion

        #region Вызовы методов

        public override StringBuilder VisitMethodOrFieldSelector(JavaGrammarParser.MethodOrFieldSelectorContext context)
        {
            if (context.identifier() != null)
            {
                string methodName = context.identifier().GetText();

                // Специальная обработка для System.out.println и System.out.print
                // В постпроцессе заменяем на print, здесь просто оставляем как есть
                Append($".{methodName}");

                // Аргументы
                if (context.arguments() != null)
                {
                    Visit(context.arguments());
                }
            }

            return _output;
        }

        public override StringBuilder VisitArguments(JavaGrammarParser.ArgumentsContext context)
        {
            Append("(");

            if (context.expressionList() != null)
            {
                bool first = true;
                foreach (var expr in context.expressionList().expression())
                {
                    if (!first) Append(", ");
                    first = false;
                    Visit(expr);
                }
            }

            AppendLine(")");

            return _output;
        }

        public override StringBuilder VisitArgumentsSuffix(JavaGrammarParser.ArgumentsSuffixContext context)
        {
            if (context.arguments() != null)
            {
                Visit(context.arguments());
            }

            return _output;
        }

        #endregion

        #region Блоки и области видимости

        public override StringBuilder VisitBlock(JavaGrammarParser.BlockContext context)
        {
            if (context.blockStatements() != null)
            {
                VisitBlockStatements(context.blockStatements());
            }
            else
            {
                AppendIndent();
                AppendLine("pass");
            }

            return _output;
        }

        public override StringBuilder VisitBlockStatements(JavaGrammarParser.BlockStatementsContext context)
        {
            if (context.blockStatement() != null)
            {
                foreach (var stmt in context.blockStatement())
                {
                    AppendIndent();
                    Visit(stmt);
                }
            }

            return _output;
        }

        public override StringBuilder VisitStatementBlockStatement(JavaGrammarParser.StatementBlockStatementContext context)
        {
            //AppendIndent(); Возможно
            return VisitChildren(context);
        }

        public override StringBuilder VisitLocalVariableBlockStatement(JavaGrammarParser.LocalVariableBlockStatementContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitInnerBlockStatement(JavaGrammarParser.InnerBlockStatementContext context)
        {
            // Вложенный блок - обрабатываем как обычный блок
            if (context.block() != null)
            {
                Visit(context.block());
            }

            return _output;
        }

        public override StringBuilder VisitEmptyStatement(JavaGrammarParser.EmptyStatementContext context)
        {
            AppendIndent();
            AppendLine("pass");
            return _output;
        }

        #endregion

        #region Вспомогательные методы для типизации

        private string GetExpressionType(JavaGrammarParser.ExpressionContext context)
        {
            // Упрощенная реализация определения типа выражения
            // В реальности нужно использовать семантический анализатор
            return "unknown";
        }

        private string GetExpressionType(JavaGrammarParser.Expression2Context context)
        {
            // Упрощенная реализация
            return "unknown";
        }

        #endregion

        #region Прочие методы (заглушки)

        // Остальные методы оставляем как заглушки

        public override StringBuilder VisitQualifiedIdentifier(JavaGrammarParser.QualifiedIdentifierContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitLiteral(JavaGrammarParser.LiteralContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitTypeDeclaration(JavaGrammarParser.TypeDeclarationContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitEmptyClassBodyDeclaration(JavaGrammarParser.EmptyClassBodyDeclarationContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitStaticBlockClassBodyDeclaration(JavaGrammarParser.StaticBlockClassBodyDeclarationContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitFieldOrMethodMember(JavaGrammarParser.FieldOrMethodMemberContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitFieldDeclaratorsRest(JavaGrammarParser.FieldDeclaratorsRestContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitMethodDeclaratorRest(JavaGrammarParser.MethodDeclaratorRestContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitFormalParameterDeclsRest(JavaGrammarParser.FormalParameterDeclsRestContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitVariableDeclaratorId(JavaGrammarParser.VariableDeclaratorIdContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitVariableDeclaratorRest(JavaGrammarParser.VariableDeclaratorRestContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitVariableDeclarators(JavaGrammarParser.VariableDeclaratorsContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitVariableInitializers(JavaGrammarParser.VariableInitializersContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitLabeledStatementBlockStatement(JavaGrammarParser.LabeledStatementBlockStatementContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitExpressionStatement(JavaGrammarParser.ExpressionStatementContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitStatementExpression(JavaGrammarParser.StatementExpressionContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitSwitchBlockStatementGroups(JavaGrammarParser.SwitchBlockStatementGroupsContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitSwitchBlockStatementGroup(JavaGrammarParser.SwitchBlockStatementGroupContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitSwitchLabels(JavaGrammarParser.SwitchLabelsContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitCaseExprLabel(JavaGrammarParser.CaseExprLabelContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitCaseEnumLabel(JavaGrammarParser.CaseEnumLabelContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitEnumConstantName(JavaGrammarParser.EnumConstantNameContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitForDeclarationContext(JavaGrammarParser.ForDeclarationContextContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitAssignmentOperator(JavaGrammarParser.AssignmentOperatorContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitCastExpression(JavaGrammarParser.CastExpressionContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitInfixOp(JavaGrammarParser.InfixOpContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitPrefixOp(JavaGrammarParser.PrefixOpContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitPostfixOp(JavaGrammarParser.PostfixOpContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitParExpression(JavaGrammarParser.ParExpressionContext context)
        {
            Append("(");
            if (context.expression() != null)
            {
                Visit(context.expression());
            }
            Append(")");
            return _output;
        }

        public override StringBuilder VisitClassLiteralSuffix(JavaGrammarParser.ClassLiteralSuffixContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitExpressionList(JavaGrammarParser.ExpressionListContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitArraySelector(JavaGrammarParser.ArraySelectorContext context)
        {
            Append("[");
            if (context.expression() != null)
            {
                Visit(context.expression());
            }
            Append("]");
            return _output;
        }

        public override StringBuilder VisitBasicType(JavaGrammarParser.BasicTypeContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitReferenceType(JavaGrammarParser.ReferenceTypeContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitTypeArguments(JavaGrammarParser.TypeArgumentsContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitReferenceTypeList(JavaGrammarParser.ReferenceTypeListContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitModifier(JavaGrammarParser.ModifierContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitVariableModifier(JavaGrammarParser.VariableModifierContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitTypeArgumentsOrDiamond(JavaGrammarParser.TypeArgumentsOrDiamondContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitCreatedName(JavaGrammarParser.CreatedNameContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitClassCreatorRest(JavaGrammarParser.ClassCreatorRestContext context)
        {
            return VisitChildren(context);
        }

        public override StringBuilder VisitArrayCreatorRest(JavaGrammarParser.ArrayCreatorRestContext context)
        {
            return VisitChildren(context);
        }

        #endregion
    }
}