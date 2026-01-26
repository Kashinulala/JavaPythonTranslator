using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using LexerParserLibrary;

namespace LexerParserLibrary
{
    // Класс для хранения информации о символах

    public class SymbolInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Scope { get; set; }
        public bool IsInitialized { get; set; }
        public bool IsFinal { get; set; }
        public bool IsStatic { get; set; }
    }

    // Класс таблицы символов для управления областями видимости
    public class SymbolTable
    {
        private readonly Stack<Dictionary<string, SymbolInfo>> _scopes = new Stack<Dictionary<string, SymbolInfo>>();
        private readonly Stack<string> _scopeNames = new Stack<string>();
        private readonly Dictionary<string, string> _classTypes = new Dictionary<string, string>();
        private readonly Dictionary<string, bool> _importedNames = new Dictionary<string, bool>();
        private readonly HashSet<string> _wildcardImports = new HashSet<string>();
        private readonly Dictionary<string, string> _packages = new Dictionary<string, string>();
        private readonly HashSet<string> _currentPackageClasses = new HashSet<string>();

        public void AddImport(string name, bool isStatic, bool isWildcard)
        {
            _importedNames[name] = isStatic;

            if (isWildcard)
            {
                _wildcardImports.Add(name);
            }
        }

        public bool IsImported(string name)
        {
            return _importedNames.ContainsKey(name);
        }

        public List<string> GetConflictingWildcards(string packageName)
        {
            List<string> conflicts = new List<string>();

            foreach (var wildcard in _wildcardImports)
            {
                if (wildcard.StartsWith(packageName + ".") || packageName.StartsWith(wildcard + "."))
                {
                    conflicts.Add(wildcard);
                }
            }

            return conflicts;
        }

        public bool HasPackage(string packageName)
        {
            return _packages.ContainsKey(packageName) ||
                   _packages.Values.Any(pkg => pkg.StartsWith(packageName + "."));
        }

        public bool IsClass(string className)
        {
            // Обработка стандартных классов Java
            if (IsStandardJavaClass(className))
            {
                return true;
            }

            // Обработка импортированных классов
            if (IsImportedClass(className))
            {
                return true;
            }

            // Обработка объявленных в коде классов
            if (_classTypes.ContainsKey(className))
            {
                return true;
            }

            // Обработка вложенных классов (MyClass.InnerClass)
            if (className.Contains("."))
            {
                string[] parts = className.Split('.');
                if (parts.Length > 1)
                {
                    string outerClass = parts[0];
                    string innerClass = string.Join(".", parts.Skip(1));

                    // Проверяем, существует ли внешний класс
                    if (IsClass(outerClass))
                    {
                        // Проверяем наличие вложенного класса (упрощенная логика)
                        return true; // В реальной реализации нужна более сложная проверка
                    }
                }
            }

            return false;
        }

        private bool IsStandardJavaClass(string className)
        {
            // Стандартные классы Java (можно расширить список)
            HashSet<string> standardClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Object", "String", "Integer", "Double", "Boolean", "Character",
                "Byte", "Short", "Long", "Float", "Void", "Class", "System", "Math",
                "List", "ArrayList", "Map", "HashMap", "Set", "HashSet", "Iterator",
                "Collections", "Arrays", "Thread", "Runnable", "Exception", "RuntimeException"
            };

            // Проверяем простое имя класса
            if (standardClasses.Contains(className))
            {
                return true;
            }

            // Проверяем полные имена (java.lang.String)
            if (className.StartsWith("java.") || className.StartsWith("javax."))
            {
                string simpleName = className.Substring(className.LastIndexOf('.') + 1);
                return standardClasses.Contains(simpleName);
            }

            return false;
        }

        private bool IsImportedClass(string className)
        {
            // Упрощенная проверка импортов (в реальной реализации нужна более сложная логика)
            return _importedNames.ContainsKey(className);

            // Более полная реализация должна:
            // 1. Проверять импорты с wildcard (import java.util.*)
            // 2. Проверять статические импорты
            // 3. Учитывать конфликты имен
        }

        public bool HasClassMember(string className, string memberName)
        {
            // Простая реализация для демонстрации
            if (className == "Math")
            {
                return memberName == "PI" || memberName == "E" ||
                       memberName == "sqrt" || memberName == "pow";
            }

            return false;
        }

        public bool HasClassInCurrentPackage(string className)
        {
            return _currentPackageClasses.Contains(className);
        }

        public SymbolTable()
        {
            // Глобальная область видимости
            EnterScope("global");
        }

        public void EnterScope(string scopeName)
        {
            _scopeNames.Push(scopeName);
            _scopes.Push(new Dictionary<string, SymbolInfo>());
        }

        public void ExitScope()
        {
            if (_scopes.Count > 1) // Не выходим из глобальной области
            {
                _scopes.Pop();
                _scopeNames.Pop();
            }
        }

        public string GetCurrentScope()
        {
            return _scopeNames.Peek();
        }

        public void Declare(string name, string type, bool isFinal = false, bool isStatic = false)
        {
            if (IsDeclaredInCurrentScope(name))
            {
                throw new SemanticException($"Symbol '{name}' is already declared in current scope");
            }

            var symbol = new SymbolInfo
            {
                Name = name,
                Type = type,
                Scope = GetCurrentScope(),
                IsFinal = isFinal,
                IsStatic = isStatic
            };

            _scopes.Peek()[name] = symbol;

            // Если это объявление класса, сохраняем его тип
            if (GetCurrentScope() == "global" && type == "class")
            {
                _classTypes[name] = type;
            }
        }

        public bool IsDeclared(string name)
        {
            foreach (var scope in _scopes)
            {
                if (scope.ContainsKey(name))
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsDeclaredInCurrentScope(string name)
        {
            return _scopes.Peek().ContainsKey(name);
        }

        public SymbolInfo GetSymbol(string name)
        {
            foreach (var scope in _scopes)
            {
                if (scope.TryGetValue(name, out var symbol))
                {
                    return symbol;
                }
            }
            return null;
        }
    }

    // Исключение для семантических ошибок
    public class SemanticException : Exception
    {
        public int Line { get; }
        public int Column { get; }

        public SemanticException(string message, int line = -1, int column = -1)
            : base(message)
        {
            Line = line;
            Column = column;
        }
    }

    // Семантический анализатор
    public class SemanticAnalyzer : JavaGrammarBaseVisitor<object>
    {
        private readonly SymbolTable _symbolTable = new SymbolTable();
        private readonly List<SemanticException> _errors = new List<SemanticException>();
        private readonly Stack<int> _defaultLabelCounts = new Stack<int>();
        private readonly List<SemanticException> _warnings = new List<SemanticException>();

        public IEnumerable<SemanticException> Warnings => _warnings;
        public bool HasWarnings => _warnings.Count > 0;
        public IEnumerable<SemanticException> Errors => _errors;
        public bool HasErrors => _errors.Count > 0;

        // Анализ всего файла
        public override object VisitCompilationUnit(JavaGrammarParser.CompilationUnitContext context)
        {
            try
            {
                return base.VisitCompilationUnit(context);
            }
            catch (SemanticException ex)
            {
                _errors.Add(ex);
                return null;
            }
        }

        // Обработка класса
        public override object VisitClassDeclaration(JavaGrammarParser.ClassDeclarationContext context)
        {
            string className = context.identifier().GetText();

            // Проверяем, что имя класса начинается с заглавной буквы
            if (!char.IsUpper(className[0]))
            {
                ReportError($"Class name '{className}' should start with uppercase letter", context);
            }

            // Добавляем класс в глобальную область видимости
            _symbolTable.Declare(className, "class");

            // Входим в область видимости класса
            _symbolTable.EnterScope(className);

            // Обрабатываем тело класса
            var result = Visit(context.classBody());

            // Выходим из области видимости класса
            _symbolTable.ExitScope();

            return result;
        }

        // Обработка метода
        public override object VisitMethodDeclaratorRest(JavaGrammarParser.MethodDeclaratorRestContext context)
        {
            var parentCtx = context.Parent;

            // Находим объявление метода
            JavaGrammarParser.MethodOrFieldDeclContext methodDecl = null;
            var current = parentCtx;
            while (current != null)
            {
                if (current is JavaGrammarParser.MethodOrFieldDeclContext decl)
                {
                    methodDecl = decl;
                    break;
                }
                current = current.Parent;
            }

            if (methodDecl != null)
            {
                string methodName = methodDecl.identifier().GetText();
                string returnType = GetTypeName(methodDecl.type());

                // Проверяем соглашения об именовании для методов
                if (char.IsUpper(methodName[0]))
                {
                    ReportError($"Method name '{methodName}' should start with lowercase letter", context);
                }

                // Входим в область видимости метода
                _symbolTable.EnterScope(methodName);

                // Обрабатываем параметры метода
                if (context.formalParameters() != null)
                {
                    Visit(context.formalParameters());
                }

                // Обрабатываем тело метода
                if (context.block() != null)
                {
                    Visit(context.block());
                }

                // Выходим из области видимости метода
                _symbolTable.ExitScope();
            }

            return null;
        }

        // Обработка параметров метода
        public override object VisitFormalParameterDecls(JavaGrammarParser.FormalParameterDeclsContext context)
        {
            string paramType = GetTypeName(context.type());
            var rest = context.formalParameterDeclsRest();

            if (rest != null && rest.variableDeclaratorId() != null)
            {
                string paramName = rest.variableDeclaratorId().GetText();
                _symbolTable.Declare(paramName, paramType);
            }

            return null;
        }

        // Обработка объявления переменных
        public override object VisitVariableDeclarator(JavaGrammarParser.VariableDeclaratorContext context)
        {
            string varName = context.identifier().GetText();
            var parentCtx = context.Parent;

            string varType = "unknown";
            bool isFinal = false;
            bool isStatic = false;

            // Определяем тип переменной и модификаторы из контекста
            if (parentCtx is JavaGrammarParser.FieldDeclaratorsRestContext fieldRest)
            {
                var fieldParent = fieldRest.Parent as JavaGrammarParser.MethodOrFieldRestContext;
                if (fieldParent != null)
                {
                    var methodOrFieldParent = fieldParent.Parent as JavaGrammarParser.MethodOrFieldDeclContext;
                    if (methodOrFieldParent != null)
                    {
                        varType = GetTypeName(methodOrFieldParent.type());

                        // Проверяем модификаторы
                        var current = methodOrFieldParent.Parent;
                        while (current != null)
                        {
                            if (current is JavaGrammarParser.MemberClassBodyDeclarationContext memberDecl)
                            {
                                foreach (var modifier in memberDecl.modifier())
                                {
                                    if (modifier.GetText() == "final")
                                        isFinal = true;
                                    if (modifier.GetText() == "static")
                                        isStatic = true;
                                }
                                break;
                            }
                            current = current.Parent as ParserRuleContext;
                        }
                    }
                }
            }
            else if (parentCtx is JavaGrammarParser.VariableDeclaratorsContext varDecls)
            {
                var localVarDecl = GetParentOfType<JavaGrammarParser.LocalVariableDeclarationStatementContext>(varDecls);
                if (localVarDecl != null)
                {
                    varType = GetTypeName(localVarDecl.type());
                }
            }

            try
            {
                // Проверяем дублирование имен
                if (_symbolTable.IsDeclaredInCurrentScope(varName))
                {
                    ReportError($"Variable '{varName}' is already declared in this scope", context);
                }
                else
                {
                    _symbolTable.Declare(varName, varType, isFinal, isStatic);
                }

                // Проверяем инициализацию final переменных
                if (context.variableDeclaratorRest().variableInitializer() != null)
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

        // Обработка использования переменных (идентификаторов)
        public override object VisitIdentifier(JavaGrammarParser.IdentifierContext context)
        {
            string name = context.GetText();

            // Не проверяем специальные идентификаторы типа "class", "this", "super"
            if (name == "class" || name == "this" || name == "super")
            {
                return null;
            }

            // Проверяем, что переменная объявлена
            if (!_symbolTable.IsDeclared(name))
            {
                // Дополнительная проверка для констант вроде System.out
                if (!name.Contains("."))
                {
                    ReportError($"Variable '{name}' is used but not declared", context);
                }
            }

            return null;
        }

        // Обработка выражений присваивания
        public override object VisitAssignmentExpression(JavaGrammarParser.AssignmentExpressionContext context)
        {
            // Проверяем левую часть присваивания
            var left = context.expression(0);

            // Проверяем, является ли левая часть выражением, содержащим идентификатор
            if (left is JavaGrammarParser.PrimaryExpressionContext primaryExpr)
            {
                string varName = null;

                var expr1 = primaryExpr.expression1();

                if (expr1 != null)
                {
                    // Получаем первый expression2 из expression1
                    JavaGrammarParser.Expression2Context expr2 = null;

                    if (expr1 is JavaGrammarParser.SimpleExpression2Context simpleExpr2)
                    {
                        expr2 = simpleExpr2.expression2();
                    }
                    else if (expr1 is JavaGrammarParser.InfixExpressionContext infixExpr &&
                             infixExpr.expression2().Length > 0)
                    {
                        expr2 = infixExpr.expression2()[0];
                    }

                    // Проверяем, является ли expr2 PostfixExpressionContext
                    if (expr2 is JavaGrammarParser.PostfixExpressionContext postfixExpr)
                    {
                        var primary = postfixExpr.primary();

                        if (primary is JavaGrammarParser.IdentifierPrimaryContext identifierPrimary &&
                            identifierPrimary.identifier().Length > 0)
                        {
                            varName = identifierPrimary.identifier()[0].GetText();
                        }
                    }
                }

                if (varName != null)
                {
                    // Проверяем, существует ли переменная
                    var symbol = _symbolTable.GetSymbol(varName);
                    if (symbol == null)
                    {
                        ReportError($"Variable '{varName}' is not declared", left);
                    }
                    else if (symbol.IsFinal && symbol.IsInitialized)
                    {
                        // Проверяем, что final переменные не переинициализируются
                        ReportError($"Final variable '{varName}' cannot be reassigned", left);
                    }
                }
            }

            // Обрабатываем обе части выражения
            Visit(context.expression(0));
            Visit(context.expression(1));

            return null;
        }

        // Обработка блоков кода (новая область видимости)
        public override object VisitBlock(JavaGrammarParser.BlockContext context)
        {
            string scopeName = $"block_{context.Start.Line}_{context.Start.Column}";
            _symbolTable.EnterScope(scopeName);
            var result = base.VisitBlock(context);
            _symbolTable.ExitScope();
            return result;
        }

        // Обработка статических блоков
        public override object VisitStaticBlockClassBodyDeclaration(JavaGrammarParser.StaticBlockClassBodyDeclarationContext context)
        {
            _symbolTable.EnterScope("static_initializer");
            var result = base.VisitStaticBlockClassBodyDeclaration(context);
            _symbolTable.ExitScope();
            return result;
        }

        // Помощник для получения родительского контекста определенного типа
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

        // Помощник для получения типа из контекста
        private string GetTypeName(JavaGrammarParser.TypeContext context)
        {
            if (context == null) return "unknown";

            // Проверяем, является ли это базовым типом
            if (context is JavaGrammarParser.BasicTypeTypeContext basicTypeCtx &&
                basicTypeCtx.basicType() != null)
            {
                return basicTypeCtx.basicType().GetText();
            }

            // Проверяем, является ли это ссылочным типом
            if (context is JavaGrammarParser.ReferenceTypeTypeContext refTypeCtx &&
                refTypeCtx.referenceType() != null)
            {
                var refType = refTypeCtx.referenceType();
                if (refType.identifier().Length > 0)
                {
                    return refType.identifier()[0].GetText();
                }
            }

            return "unknown";
        }

        // Сообщение об ошибке
        private void ReportError(string message, ParserRuleContext context)
        {
            int line = context.Start.Line;
            int column = context.Start.Column;
            _errors.Add(new SemanticException(message, line, column));
        }

        private void ReportWarning(string message, ParserRuleContext context)
        {
            int line = context.Start.Line;
            int column = context.Start.Column;
            _warnings.Add(new SemanticException(message, line, column));
        }

        public void GenerateReport(string outputPath = "semantic_report.json", string fileName = "unknown")
        {
            var report = new SemanticReport
            {
                FileName = fileName
            };

            // Добавляем ошибки в отчет
            if (HasErrors)
            {
                foreach (var error in _errors)
                {
                    report.Errors.Add(new ReportItem
                    {
                        Line = error.Line,
                        Column = error.Column,
                        Message = error.Message,
                        Severity = "Error"
                    });
                }
            }

            // Добавляем предупреждения в отчет
            if (HasWarnings)
            {
                foreach (var warning in _warnings)
                {
                    report.Warnings.Add(new ReportItem
                    {
                        Line = warning.Line,
                        Column = warning.Column,
                        Message = warning.Message,
                        Severity = "Warning"
                    });
                }
            }

            // Сохраняем отчет в JSON файл
            try
            {
                string json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(outputPath, json);
                Console.WriteLine($"Semantic analysis report saved to: {Path.GetFullPath(outputPath)}");

                // Также выводим краткую сводку в консоль
                Console.WriteLine("\n===== SEMANTIC ANALYSIS SUMMARY =====");
                Console.WriteLine($"File: {fileName}");
                Console.WriteLine($"Errors found: {report.Errors.Count}");
                Console.WriteLine($"Warnings found: {report.Warnings.Count}");

                if (report.Errors.Count > 0)
                {
                    Console.WriteLine("\nERRORS:");
                    foreach (var error in report.Errors)
                    {
                        Console.WriteLine($"Line {error.Line}, Column {error.Column}: {error.Message}");
                    }
                }

                if (report.Warnings.Count > 0)
                {
                    Console.WriteLine("\nWARNINGS:");
                    foreach (var warning in report.Warnings)
                    {
                        Console.WriteLine($"Line {warning.Line}, Column {warning.Column}: {warning.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save semantic report: {ex.Message}");
            }
        }

        // Анализ базовых типов
        public override object VisitBasicType(JavaGrammarParser.BasicTypeContext context)
        {
            string typeName = context.GetText().ToLower();
            return typeName;
        }

        // Анализ ссылочных типов
        public override object VisitReferenceType(JavaGrammarParser.ReferenceTypeContext context)
        {
            if (context.identifier().Length > 0)
            {
                string typeName = context.identifier()[0].GetText();
                return typeName;
            }
            return "unknown";
        }

        // Анализ условных операторов if
        public override object VisitIfStatement(JavaGrammarParser.IfStatementContext context)
        {
            // Проверяем тип условия в if
            var condition = context.parExpression().expression();
            string conditionType = GetExpressionType(condition);

            if (conditionType != "boolean")
            {
                ReportError($"If condition must be of boolean type, found: {conditionType}", context);
            }

            // Рекурсивно обрабатываем ветки then/else
            Visit(context.statement(0)); // then branch

            if (context.ELSE() != null && context.statement().Length > 1)
            {
                Visit(context.statement(1)); // else branch
            }

            return null;
        }

        // Анализ цикла for
        public override object VisitForStatement(JavaGrammarParser.ForStatementContext context)
        {
            var forControl = context.forControl();

            // Обрабатываем тело цикла
            Visit(context.statement());

            return null;
        }

        // Анализ цикла while
        public override object VisitWhileStatement(JavaGrammarParser.WhileStatementContext context)
        {
            // Проверяем тип условия в while
            var condition = context.parExpression().expression();
            string conditionType = GetExpressionType(condition);

            if (conditionType != "boolean")
            {
                ReportError($"While condition must be of boolean type, found: {conditionType}", context);
            }

            // Обрабатываем тело цикла
            Visit(context.statement());

            return null;
        }

        // Анализ оператора return
        public override object VisitReturnStatement(JavaGrammarParser.ReturnStatementContext context)
        {
            // Проверяем, что return находится внутри метода
            var currentScope = _symbolTable.GetCurrentScope();
            if (currentScope == "global" || currentScope.StartsWith("block_") || currentScope == "static_initializer")
            {
                ReportError("Return statement not allowed outside of a method", context);
                return base.VisitReturnStatement(context);
            }

            // Если есть выражение после return
            if (context.expression() != null)
            {
                string returnType = GetExpressionType(context.expression());
                string methodReturnType = GetMethodReturnType(currentScope);

                if (methodReturnType == "void")
                {
                    ReportError("Cannot return a value from a void method", context);
                }
                else if (!AreTypesCompatible(returnType, methodReturnType))
                {
                    ReportError($"Incompatible return type: {returnType} cannot be converted to {methodReturnType}", context);
                }
            }
            else
            {
                // Нет выражения после return
                string methodReturnType = GetMethodReturnType(currentScope);
                if (methodReturnType != "void" && !IsVoidEquivalentType(methodReturnType))
                {
                    ReportError($"Missing return value for method with return type {methodReturnType}", context);
                }
            }

            return base.VisitReturnStatement(context);
        }

        // Анализ создания объектов через new
        public override object VisitNewCreatorPrimary(JavaGrammarParser.NewCreatorPrimaryContext context)
        {
            if (context.creator() != null)
            {
                var createdName = context.creator().createdName();
                if (createdName != null && createdName.identifier().Length > 0)
                {
                    string className = createdName.identifier()[0].GetText();
                    if (!_symbolTable.IsClass(className))
                    {
                        ReportError($"Class '{className}' is not declared", context);
                    }
                }
            }

            return base.VisitNewCreatorPrimary(context);
        }

        // Вспомогательный метод для определения типа выражения
        private string GetExpressionType(JavaGrammarParser.ExpressionContext context)
        {
            if (context == null) return "unknown";

            // Для литералов определяем тип напрямую
            if (context is JavaGrammarParser.PrimaryExpressionContext primaryExpr)
            {
                var expr1 = primaryExpr.expression1();

                if (expr1 != null)
                {
                    JavaGrammarParser.Expression2Context expr2 = null;

                    if (expr1 is JavaGrammarParser.SimpleExpression2Context simpleExpr2)
                    {
                        expr2 = simpleExpr2.expression2();
                    }
                    else if (expr1 is JavaGrammarParser.InfixExpressionContext infixExpr &&
                             infixExpr.expression2().Length > 0)
                    {
                        expr2 = infixExpr.expression2()[0];
                    }

                    if (expr2 is JavaGrammarParser.PostfixExpressionContext postfixExpr)
                    {
                        var primary = postfixExpr.primary();

                        if (primary is JavaGrammarParser.LiteralPrimaryContext literalPrimary)
                        {
                            var literal = literalPrimary.literal();
                            if (literal.INTEGER_LITERAL() != null) return "int";
                            if (literal.FLOATING_POINT_LITERAL() != null) return "double";
                            if (literal.CHARACTER_LITERAL() != null) return "char";
                            if (literal.STRING_LITERAL() != null) return "String";
                            if (literal.TRUE() != null || literal.FALSE() != null) return "boolean";
                            if (literal.NULL() != null) return "null";
                        }
                    }
                }
            }

            // Для идентификаторов ищем в таблице символов
            if (context.GetText().All(c => char.IsLetterOrDigit(c) || c == '_'))
            {
                var symbol = _symbolTable.GetSymbol(context.GetText());
                if (symbol != null)
                {
                    return symbol.Type;
                }
            }

            return "unknown";
        }

        // Анализ аргументов типа в дженериках (например, <String, Integer>)
        public override object VisitTypeArguments(JavaGrammarParser.TypeArgumentsContext context)
        {
            // Проверяем каждый тип в списке аргументов
            var referenceTypeList = context.referenceTypeList();
            if (referenceTypeList != null)
            {
                foreach (var refType in referenceTypeList.referenceType())
                {
                    if (refType.identifier().Length > 0)
                    {
                        string typeName = refType.identifier()[0].GetText();
                        if (!_symbolTable.IsClass(typeName) && typeName != "T" && typeName != "E" && typeName != "K" && typeName != "V")
                        {
                            // Проверяем только конкретные классы, не параметры типа
                            ReportError($"Type '{typeName}' is not declared", refType);
                        }
                    }
                }
            }

            return base.VisitTypeArguments(context);
        }

        // Анализ инфиксных выражений (a + b, a && b, a == b и т.д.)
        public override object VisitInfixExpression(JavaGrammarParser.InfixExpressionContext context)
        {
            // Сначала анализируем все операнды
            var expr2List = context.expression2();
            if (expr2List == null || expr2List.Length < 2)
                return base.VisitInfixExpression(context);

            // Получаем типы левого и правого операндов
            string leftType = GetExpression2Type(expr2List[0]);
            string rightType = GetExpression2Type(expr2List[1]);

            // Проверяем операторы
            var infixOps = context.infixOp();
            foreach (var op in infixOps)
            {
                string operatorText = op.GetText();

                // Проверка совместимости типов для операторов
                switch (operatorText)
                {
                    case "&&":
                    case "||":
                        // Логические операторы работают только с boolean
                        if (leftType != "boolean" || rightType != "boolean")
                        {
                            ReportError($"Logical operator '{operatorText}' requires boolean operands, found {leftType} and {rightType}", op);
                        }
                        break;

                    case "==":
                    case "!=":
                        // Операторы равенства могут работать с совместимыми типами
                        if (!AreTypesCompatible(leftType, rightType))
                        {
                            ReportError($"Incompatible types for comparison: {leftType} and {rightType}", op);
                        }
                        break;

                    case "<":
                    case ">":
                    case "<=":
                    case ">=":
                        // Операторы сравнения работают только с числовыми типами
                        if (!IsNumericType(leftType) || !IsNumericType(rightType))
                        {
                            ReportError($"Relational operator '{operatorText}' requires numeric operands, found {leftType} and {rightType}", op);
                        }
                        break;

                    case "+":
                        // Сложение может быть числовой операцией или конкатенацией строк
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
                        // Арифметические операторы работают только с числовыми типами
                        if (!IsNumericType(leftType) || !IsNumericType(rightType))
                        {
                            ReportError($"Arithmetic operator '{operatorText}' requires numeric operands, found {leftType} and {rightType}", op);
                        }
                        break;
                }
            }

            return base.VisitInfixExpression(context);
        }

        private string GetExpression2Type(JavaGrammarParser.Expression2Context context)
        {
            if (context == null) return "unknown";

            // Для простоты определяем тип по литералам
            if (context is JavaGrammarParser.PostfixExpressionContext postfixExpr)
            {
                var primary = postfixExpr.primary();

                if (primary is JavaGrammarParser.LiteralPrimaryContext literalPrimary)
                {
                    var literal = literalPrimary.literal();
                    if (literal.INTEGER_LITERAL() != null) return "int";
                    if (literal.FLOATING_POINT_LITERAL() != null) return "double";
                    if (literal.CHARACTER_LITERAL() != null) return "char";
                    if (literal.STRING_LITERAL() != null) return "String";
                    if (literal.TRUE() != null || literal.FALSE() != null) return "boolean";
                    if (literal.NULL() != null) return "null";
                }

                // Если это идентификатор, пытаемся найти его в таблице символов
                if (primary is JavaGrammarParser.IdentifierPrimaryContext idPrimary &&
                    idPrimary.identifier().Length > 0)
                {
                    string varName = idPrimary.identifier()[0].GetText();
                    var symbol = _symbolTable.GetSymbol(varName);
                    return symbol?.Type ?? "unknown";
                }
            }

            return "unknown";
        }

        // Анализ префиксных выражений (!a, +a, -a и т.д.)
        // Анализ префиксных выражений (!a, +a, -a и т.д.)
        public override object VisitPrefixExpression(JavaGrammarParser.PrefixExpressionContext context)
        {
            var prefixOp = context.prefixOp();
            var expr2 = context.expression2();

            if (prefixOp == null || expr2 == null)
                return base.VisitPrefixExpression(context);

            string operatorText = prefixOp.GetText();
            string operandType = GetExpression2Type(expr2);

            switch (operatorText)
            {
                case "!":
                    // Логическое НЕ работает только с boolean
                    if (operandType != "boolean")
                    {
                        ReportError($"Operator '!' requires boolean operand, found {operandType}", prefixOp);
                    }
                    break;

                case "+":
                case "-":
                    // Унарные плюс и минус работают только с числовыми типами
                    if (!IsNumericType(operandType))
                    {
                        ReportError($"Unary operator '{operatorText}' requires numeric operand, found {operandType}", prefixOp);
                    }
                    break;
            }

            return base.VisitPrefixExpression(context);
        }

        // Анализ постфиксных выражений (a++, a--)
        public override object VisitPostfixExpression(JavaGrammarParser.PostfixExpressionContext context)
        {
            var primary = context.primary();
            var postfixOp = context.postfixOp();

            if (postfixOp == null || primary == null)
                return base.VisitPostfixExpression(context);

            string operatorText = postfixOp.GetText();
            string exprType = "unknown";

            // Определяем тип основного выражения
            if (primary is JavaGrammarParser.IdentifierPrimaryContext idPrimary &&
                idPrimary.identifier().Length > 0)
            {
                string varName = idPrimary.identifier()[0].GetText();
                var symbol = _symbolTable.GetSymbol(varName);

                if (symbol != null)
                {
                    exprType = symbol.Type;

                    // Проверяем, что переменная не final для инкремента/декремента
                    if (symbol.IsFinal)
                    {
                        ReportError($"Cannot modify final variable '{varName}' with operator '{operatorText}'", postfixOp);
                    }
                }
                else
                {
                    ReportError($"Variable '{varName}' is not declared", primary);
                }
            }

            // Инкремент/декремент работают только с числовыми типами
            if (!IsNumericType(exprType))
            {
                ReportError($"Postfix operator '{operatorText}' requires numeric operand, found {exprType}", postfixOp);
            }

            return base.VisitPostfixExpression(context);
        }

        // Вспомогательные методы

        private bool IsNumericType(string type)
        {
            if (type == null) return false;

            type = type.ToLower();
            return type == "int" || type == "long" || type == "float" ||
                   type == "double" || type == "byte" || type == "short" ||
                   type == "char";
        }

        private bool AreTypesCompatible(string type1, string type2)
        {
            if (type1 == null || type2 == null) return false;

            // Приводим к нижнему регистру для сравнения
            type1 = type1.ToLower();
            type2 = type2.ToLower();

            // Одинаковые типы всегда совместимы
            if (type1 == type2) return true;

            // Числовые типы совместимы между собой
            if (IsNumericType(type1) && IsNumericType(type2)) return true;

            // Специальные случаи приведения
            if ((type1 == "string" && type2 == "null") ||
                (type1 == "null" && type2 == "string"))
            {
                return true;
            }

            return false;
        }

        // Анализ создания объектов и массивов
        public override object VisitCreator(JavaGrammarParser.CreatorContext context)
        {
            if (context.createdName() != null)
            {
                Visit(context.createdName());
            }

            if (context.classCreatorRest() != null)
            {
                Visit(context.classCreatorRest());
            }

            if (context.arrayCreatorRest() != null)
            {
                Visit(context.arrayCreatorRest());
            }

            return base.VisitCreator(context);
        }

        // Анализ имени создаваемого класса
        public override object VisitCreatedName(JavaGrammarParser.CreatedNameContext context)
        {
            if (context.identifier().Length > 0)
            {
                string className = context.identifier()[0].GetText();

                // Проверяем, существует ли класс
                if (!_symbolTable.IsClass(className))
                {
                    ReportError($"Class '{className}' is not declared", context);
                }

                // Если есть дженерики, проверяем их для каждого идентификатора
                var typeArgsList = context.typeArgumentsOrDiamond();
                if (typeArgsList != null && typeArgsList.Length > 0)
                {
                    // Проверяем дженерики для первого идентификатора
                    Visit(typeArgsList[0]);

                    // Проверяем дженерики для остальных идентификаторов
                    for (int i = 1; i < typeArgsList.Length; i++)
                    {
                        if (i < context.identifier().Length)
                        {
                            Visit(typeArgsList[i]);
                        }
                    }
                }

                // Проверяем квалифицированные имена (например, java.util.List)
                for (int i = 1; i < context.identifier().Length; i++)
                {
                    string part = context.identifier()[i].GetText();
                    // Дополнительные проверки для внутренних классов можно добавить здесь
                }
            }

            return base.VisitCreatedName(context);
        }

        // Анализ создания объектов классов с аргументами конструктора
        public override object VisitClassCreatorRest(JavaGrammarParser.ClassCreatorRestContext context)
        {
            // Проверяем аргументы конструктора
            if (context.arguments() != null)
            {
                var expressionList = context.arguments().expressionList();
                if (expressionList != null)
                {
                    foreach (var expr in expressionList.expression())
                    {
                        string exprType = GetExpressionType(expr);
                        // Здесь можно добавить проверку соответствия параметров конструктору,
                        // но для этого нужна более сложная система типов
                    }
                }
            }

            // Проверяем анонимные классы
            if (context.classBody() != null)
            {
                // Для анонимных классов создаём новую область видимости
                string anonClassName = $"AnonymousClass_{context.Start.Line}_{context.Start.Column}";
                _symbolTable.EnterScope(anonClassName);
                Visit(context.classBody());
                _symbolTable.ExitScope();
            }

            return base.VisitClassCreatorRest(context);
        }

        // Анализ создания массивов
        public override object VisitArrayCreatorRest(JavaGrammarParser.ArrayCreatorRestContext context)
        {
            // Случай: new int[] {1, 2, 3}
            if (context.arrayInitializer() != null)
            {
                Visit(context.arrayInitializer());
            }
            // Случай: new int[5]
            else if (context.expression(0) != null)
            {
                // Проверяем, что размер массива - целое число
                string sizeType = GetExpressionType(context.expression(0));
                if (sizeType != "int")
                {
                    ReportError($"Array size must be an integer, found {sizeType}", context.expression(0));
                }

                // Если есть дополнительные размеры (многомерные массивы)
                if (context.expression().Length > 1)
                {
                    for (int i = 1; i < context.expression().Length; i++)
                    {
                        string dimType = GetExpressionType(context.expression(i));
                        if (dimType != "int")
                        {
                            ReportError($"Array dimension must be an integer, found {dimType}", context.expression(i));
                        }
                    }
                }
            }

            return base.VisitArrayCreatorRest(context);
        }

        // Анализ инициализаторов массивов
        public override object VisitArrayInitializer(JavaGrammarParser.ArrayInitializerContext context)
        {
            // Проверяем элементы массива
            var variableInitializers = context.variableInitializers();
            if (variableInitializers != null)
            {
                string arrayType = "unknown";
                bool firstElement = true;

                foreach (var initializer in variableInitializers.variableInitializer())
                {
                    string elementType = "unknown";

                    if (initializer.expression() != null)
                    {
                        elementType = GetExpressionType(initializer.expression());
                    }
                    else if (initializer.arrayInitializer() != null)
                    {
                        elementType = "array"; // Массив в массиве
                    }

                    if (firstElement)
                    {
                        arrayType = elementType;
                        firstElement = false;
                    }
                    else if (elementType != arrayType)
                    {
                        // Проверяем совместимость типов элементов массива
                        if (!AreTypesCompatible(elementType, arrayType))
                        {
                            ReportError($"Incompatible types in array initializer: {arrayType} and {elementType}", initializer);
                        }
                    }
                }
            }

            return base.VisitArrayInitializer(context);
        }

        // Анализ тела класса
        public override object VisitClassBody(JavaGrammarParser.ClassBodyContext context)
        {
            // Обработка всех объявлений в теле класса
            foreach (var declaration in context.classBodyDeclaration())
            {
                Visit(declaration);
            }

            // Проверка, есть ли конструктор по умолчанию, если нет других конструкторов
            if (_symbolTable.GetCurrentScope() != "global")
            {
                string className = _symbolTable.GetCurrentScope();
                var constructors = GetClassConstructors(className);

                if (constructors.Count == 0)
                {
                    // Добавляем неявный конструктор по умолчанию
                    _symbolTable.Declare($"{className}_default_constructor", "constructor", isStatic: false);
                }
            }

            return base.VisitClassBody(context);
        }

        // Анализ объявления члена класса
        public override object VisitMemberClassBodyDeclaration(JavaGrammarParser.MemberClassBodyDeclarationContext context)
        {
            // Собираем модификаторы
            List<string> modifiers = new List<string>();
            bool isStatic = false;
            bool isFinal = false;

            foreach (var modifier in context.modifier())
            {
                string modText = modifier.GetText();
                modifiers.Add(modText);

                if (modText == "static") isStatic = true;
                if (modText == "final") isFinal = true;
            }

            // Обрабатываем объявление члена
            var memberDecl = context.memberDecl();
            if (memberDecl != null)
            {
                if (memberDecl is JavaGrammarParser.FieldOrMethodMemberContext fieldOrMethod)
                {
                    var methodOrFieldDecl = fieldOrMethod.methodOrFieldDecl();
                    if (methodOrFieldDecl != null)
                    {
                        string memberName = methodOrFieldDecl.identifier().GetText();
                        string memberType = GetTypeName(methodOrFieldDecl.type());

                        // Проверяем дублирование имен в области видимости класса
                        if (_symbolTable.IsDeclaredInCurrentScope(memberName))
                        {
                            ReportError($"Duplicate member name '{memberName}' in class", context);
                        }
                        else
                        {
                            // Добавляем в таблицу символов
                            _symbolTable.Declare(memberName, memberType, isFinal, isStatic);
                        }
                    }
                }
                else if (memberDecl is JavaGrammarParser.VoidMethodMemberContext voidMethod)
                {
                    string methodName = voidMethod.identifier().GetText();

                    // Проверяем дублирование имен методов
                    if (_symbolTable.IsDeclaredInCurrentScope(methodName))
                    {
                        // Проверяем перегрузку - методы с одинаковыми именами, но разными параметрами разрешены
                        if (!IsMethodOverloaded(methodName, voidMethod.voidMethodDeclaratorRest().formalParameters()))
                        {
                            ReportError($"Duplicate method name '{methodName}' in class", context);
                        }
                    }
                    else
                    {
                        _symbolTable.Declare(methodName, "void", isFinal, isStatic);
                    }
                }
            }

            return base.VisitMemberClassBodyDeclaration(context);
        }

        // Вспомогательные методы

        private string GetMethodReturnType(string methodName)
        {
            var symbol = _symbolTable.GetSymbol(methodName);
            return symbol?.Type ?? "unknown";
        }

        private bool IsVoidEquivalentType(string type)
        {
            return type == "void" || type == "Void" || type == "java.lang.Void";
        }

        private List<string> GetClassConstructors(string className)
        {
            // В реальной реализации здесь должна быть логика поиска конструкторов
            return new List<string>();
        }

        private bool IsMethodOverloaded(string methodName, JavaGrammarParser.FormalParametersContext parameters)
        {
            // В реальной реализации здесь должна быть проверка сигнатур методов
            return true; // Упрощенная реализация
        }

        // Анализ цикла do-while
        public override object VisitDoWhileStatement(JavaGrammarParser.DoWhileStatementContext context)
        {
            // Проверяем тип условия в do-while
            var condition = context.parExpression().expression();
            string conditionType = GetExpressionType(condition);

            if (conditionType != "boolean")
            {
                ReportError($"Do-while condition must be of boolean type, found: {conditionType}", context);
            }

            // Обрабатываем тело цикла
            Visit(context.statement());

            return base.VisitDoWhileStatement(context);
        }

        // Анализ оператора break
        public override object VisitBreakStatement(JavaGrammarParser.BreakStatementContext context)
        {
            // Проверяем, что break используется внутри цикла или switch
            if (!IsInsideLoopOrSwitch())
            {
                ReportError("Break statement must be inside a loop or switch statement", context);
            }

            // Если есть метка, проверяем ее существование
            if (context.identifier() != null)
            {
                string labelName = context.identifier().GetText();
                if (!_symbolTable.IsDeclared(labelName))
                {
                    ReportError($"Label '{labelName}' is not declared", context);
                }
            }

            return base.VisitBreakStatement(context);
        }

        // Анализ оператора continue
        public override object VisitContinueStatement(JavaGrammarParser.ContinueStatementContext context)
        {
            // Проверяем, что continue используется внутри цикла
            if (!IsInsideLoop())
            {
                ReportError("Continue statement must be inside a loop", context);
            }

            // Если есть метка, проверяем ее существование
            if (context.identifier() != null)
            {
                string labelName = context.identifier().GetText();
                if (!_symbolTable.IsDeclared(labelName))
                {
                    ReportError($"Label '{labelName}' is not declared", context);
                }
            }

            return base.VisitContinueStatement(context);
        }

        // Анализ оператора switch
        // Анализ оператора switch
        public override object VisitSwitchStatement(JavaGrammarParser.SwitchStatementContext context)
        {
            // Добавляем новый счетчик для текущего switch-блока
            _defaultLabelCounts.Push(0);
            try
            {
                // Проверяем тип выражения в switch
                string switchType = GetExpressionType(context.parExpression().expression());

                if (!IsValidSwitchType(switchType))
                {
                    ReportError($"Invalid type for switch expression: {switchType}. Valid types are: int, char, byte, short, String, enum", context);
                }

                // Обрабатываем группы операторов в switch
                Visit(context.switchBlockStatementGroups());

                return base.VisitSwitchStatement(context);
            }
            finally
            {
                // Удаляем счетчик при выходе из switch-блока
                if (_defaultLabelCounts.Count > 0)
                {
                    _defaultLabelCounts.Pop();
                }
            }
        }

        // Вспомогательные методы

        private bool IsInsideLoopOrSwitch()
        {
            string currentScope = _symbolTable.GetCurrentScope();
            return currentScope.Contains("for") || currentScope.Contains("while") || currentScope.Contains("do") || currentScope.Contains("switch");
        }

        private bool IsInsideLoop()
        {
            string currentScope = _symbolTable.GetCurrentScope();
            return currentScope.Contains("for") || currentScope.Contains("while") || currentScope.Contains("do");
        }

        private bool IsValidSwitchType(string type)
        {
            if (type == null) return false;

            type = type.ToLower();
            return type == "int" || type == "char" || type == "byte" || type == "short" ||
                   type == "string" || type.EndsWith("enum") || type.Contains("enum");
        }

        // Анализ групп операторов в блоке switch
        public override object VisitSwitchBlockStatementGroups(JavaGrammarParser.SwitchBlockStatementGroupsContext context)
        {
            // Проверяем наличие дублирующихся case-меток
            HashSet<string> caseValues = new HashSet<string>();

            foreach (var group in context.switchBlockStatementGroup())
            {
                foreach (var label in group.switchLabels().switchLabel())
                {
                    // Проверяем тип метки с помощью оператора is
                    if (label is JavaGrammarParser.CaseExprLabelContext caseExprLabel &&
                        caseExprLabel.expression() != null)
                    {
                        string caseValue = caseExprLabel.expression().GetText();
                        if (!caseValues.Add(caseValue))
                        {
                            ReportError($"Duplicate case label: {caseValue}", label);
                        }
                    }
                    else if (label is JavaGrammarParser.DefaultLabelContext)
                    {
                        if (!caseValues.Add("default"))
                        {
                            ReportError("Duplicate default label in switch statement", label);
                        }
                    }
                }

                // Обрабатываем операторы в группе
                Visit(group.blockStatements());
            }

            // Проверяем, что все пути выполнения завершаются оператором break или return
            CheckSwitchFallThrough(context);

            return base.VisitSwitchBlockStatementGroups(context);
        }

        // Проверка возможного fall-through в switch
        private void CheckSwitchFallThrough(JavaGrammarParser.SwitchBlockStatementGroupsContext context)
        {
            // Получаем все группы case
            var groups = context.switchBlockStatementGroup();

            if (groups != null && groups.Length > 0)
            {
                // Берем последнюю группу
                var lastGroup = groups[groups.Length - 1];

                // Получаем все операторы в последней группе
                var blockStatements = lastGroup.blockStatements();
                if (blockStatements != null)
                {
                    var statements = blockStatements.blockStatement();

                    if (statements != null && statements.Length > 0)
                    {
                        // Берем последний оператор в группе
                        var lastStatement = statements[statements.Length - 1];

                        bool hasBreakOrReturn = false;

                        // Проверяем тип последнего оператора
                        if (lastStatement is JavaGrammarParser.StatementBlockStatementContext stmtBlock)
                        {
                            var statementCtx = stmtBlock.statement();

                            if (statementCtx != null)
                            {
                                // Проверяем, является ли последний оператор break или return
                                if (statementCtx is JavaGrammarParser.BreakStatementContext ||
                                    statementCtx is JavaGrammarParser.ReturnStatementContext)
                                {
                                    hasBreakOrReturn = true;
                                }
                            }
                        }
                        // Проверяем другие возможные типы операторов
                        else if (lastStatement is JavaGrammarParser.LocalVariableBlockStatementContext localVarBlock)
                        {
                            // Для локальных переменных проверяем следующий оператор в блоке
                            // Это упрощенная реализация для демонстрации
                        }
                        else if (lastStatement is JavaGrammarParser.LabeledStatementBlockStatementContext labeledBlock)
                        {
                            // Для помеченных операторов проверяем их содержимое
                        }

                        if (!hasBreakOrReturn)
                        {
                            // Это предупреждение, а не ошибка, так как fall-through иногда используется намеренно
                            // ReportWarning("Possible fall-through in switch statement", context);
                        }
                    }
                }
            }
        }

        // Анализ группы операторов в блоке switch (case ветка со всеми операторами)
        public override object VisitSwitchBlockStatementGroup(JavaGrammarParser.SwitchBlockStatementGroupContext context)
        {
            // Сначала анализируем метки case/default
            Visit(context.switchLabels());

            // Затем анализируем операторы в группе
            Visit(context.blockStatements());

            return null;
        }

        // Анализ меток case/default в switch
        public override object VisitSwitchLabels(JavaGrammarParser.SwitchLabelsContext context)
        {
            bool hasDefaultLabel = false;

            foreach (var label in context.switchLabel())
            {
                // Проверяем наличие нескольких default-меток в одной группе
                if (label is JavaGrammarParser.DefaultLabelContext)
                {
                    if (hasDefaultLabel)
                    {
                        ReportError("Duplicate default label in switch statement group", label);
                    }
                    hasDefaultLabel = true;
                }

                // Посещаем каждую метку
                Visit(label);
            }

            return null;
        }

        // Анализ метки case с выражением (case 5:)
        public override object VisitCaseExprLabel(JavaGrammarParser.CaseExprLabelContext context)
        {
            if (context.expression() != null)
            {
                string caseType = GetExpressionType(context.expression());

                // Проверяем, что тип выражения допустим в case
                if (!IsValidCaseType(caseType))
                {
                    ReportError($"Invalid type for case expression: {caseType}. Valid types are: int, char, byte, short, String, enum", context);
                }

                // Проверяем, что выражение является константой (упрощенная проверка)
                if (!IsConstantExpression(context.expression()))
                {
                    ReportError("Case expression must be a constant expression", context);
                }
            }

            return base.VisitCaseExprLabel(context);
        }

        // Анализ метки case с константой перечисления (case MY_ENUM:)
        public override object VisitCaseEnumLabel(JavaGrammarParser.CaseEnumLabelContext context)
        {
            if (context.enumConstantName() != null && context.enumConstantName().identifier() != null)
            {
                string enumName = context.enumConstantName().identifier().GetText();

                // Проверяем, что константа перечисления объявлена
                if (!_symbolTable.IsDeclared(enumName))
                {
                    ReportError($"Enum constant '{enumName}' is not declared", context);
                }
            }

            return base.VisitCaseEnumLabel(context);
        }

        // Анализ метки default
        public override object VisitDefaultLabel(JavaGrammarParser.DefaultLabelContext context)
        {
            // Проверяем, находимся ли мы внутри switch-блока
            if (_defaultLabelCounts.Count > 0)
            {
                // Получаем текущее количество default-меток для этого switch-блока
                int currentCount = _defaultLabelCounts.Pop();
                currentCount++;

                // Если уже есть default-метка, сообщаем об ошибке
                if (currentCount > 1)
                {
                    ReportError("Duplicate default label in switch statement. Only one default label is allowed per switch block.", context);
                }

                // Обновляем счетчик
                _defaultLabelCounts.Push(currentCount);
            }

            // Также проверяем, что default является последней меткой в группе
            var switchLabels = context.Parent as JavaGrammarParser.SwitchLabelsContext;
            if (switchLabels != null)
            {
                var allLabels = switchLabels.switchLabel();
                int defaultIndex = Array.IndexOf(allLabels, context);

                if (defaultIndex >= 0 && defaultIndex < allLabels.Length - 1)
                {
                    ReportError("Default label should be the last label in a switch block group for better readability and maintainability.", context);
                }
            }

            return base.VisitDefaultLabel(context);
        }

        // Вспомогательные методы
        private bool IsValidCaseType(string type)
        {
            if (type == null) return false;

            type = type.ToLower();
            return type == "int" || type == "char" || type == "byte" || type == "short" ||
                   type == "string" || type.EndsWith("enum") || type.Contains("enum");
        }

        private bool IsConstantExpression(JavaGrammarParser.ExpressionContext context)
        {
            if (context == null)
                return false;

            // Проверяем простейшие случаи - литералы
            if (context is JavaGrammarParser.PrimaryExpressionContext primaryExpr1)
            {
                var expr1 = primaryExpr1.expression1();

                if (expr1 != null)
                {
                    if (expr1 is JavaGrammarParser.SimpleExpression2Context simpleExpr2)
                    {
                        var expr2 = simpleExpr2.expression2();
                        return IsExpression2Constant(expr2);
                    }
                    // Инфиксные выражения обрабатываются отдельно ниже
                }
            }

            // Проверяем выражения с операторами
            if (context is JavaGrammarParser.AssignmentExpressionContext)
            {
                // Присваивание не может быть константным выражением
                return false;
            }

            // Проверяем выражения в скобках
            if (context.ChildCount > 2)
            {
                var firstChild = context.GetChild(0);
                var lastChild = context.GetChild(context.ChildCount - 1);

                if (firstChild != null && lastChild != null &&
                    firstChild.GetText() == "(" && lastChild.GetText() == ")")
                {
                    // Ищем выражение внутри скобок
                    for (int i = 1; i < context.ChildCount - 1; i++)
                    {
                        var child = context.GetChild(i);
                        if (child is JavaGrammarParser.ExpressionContext innerExpr)
                        {
                            return IsConstantExpression(innerExpr);
                        }
                        else if (child is JavaGrammarParser.ParExpressionContext parExpr)
                        {
                            var expr = parExpr.expression();
                            if (expr != null)
                            {
                                return IsConstantExpression(expr);
                            }
                        }
                    }
                }
            }

            // Проверяем, содержит ли выражение инфиксные операции
            if (context is JavaGrammarParser.PrimaryExpressionContext)
            {
                var primaryExpr2 = context as JavaGrammarParser.PrimaryExpressionContext;
                var expr1 = primaryExpr2.expression1();

                if (expr1 != null && expr1 is JavaGrammarParser.InfixExpressionContext infixExpr1)
                {
                    return IsInfixExpressionConstant(infixExpr1);
                }

                // Проверяем префиксные выражения через expression2
                if (expr1 != null)
                {
                    JavaGrammarParser.Expression2Context expr2 = null;

                    if (expr1 is JavaGrammarParser.SimpleExpression2Context simpleExpr2)
                    {
                        expr2 = simpleExpr2.expression2();
                    }
                    else if (expr1 is JavaGrammarParser.InfixExpressionContext infixExpr2)
                    {
                        if (infixExpr2.expression2().Length > 0)
                        {
                            expr2 = infixExpr2.expression2()[0];
                        }
                    }

                    if (expr2 != null && expr2 is JavaGrammarParser.PrefixExpressionContext prefixExpr)
                    {
                        return IsExpression2Constant(expr2);
                    }
                }
            }

            return false;
        }

        // Оставшиеся методы остаются без изменений
        private bool IsExpression2Constant(JavaGrammarParser.Expression2Context context)
        {
            if (context == null) return false;

            JavaGrammarParser.PostfixExpressionContext postfixExpr = context as JavaGrammarParser.PostfixExpressionContext;
            if (postfixExpr != null)
            {
                var primary = postfixExpr.primary();

                if (primary != null)
                {
                    // Литералы всегда являются константами
                    if (primary is JavaGrammarParser.LiteralPrimaryContext)
                    {
                        return true;
                    }

                    // Проверяем идентификаторы - это могут быть константные переменные
                    if (primary is JavaGrammarParser.IdentifierPrimaryContext idPrimary)
                    {
                        if (idPrimary.identifier().Length > 0)
                        {
                            string varName = idPrimary.identifier()[0].GetText();
                            var symbol = _symbolTable.GetSymbol(varName);

                            // Константные переменные (final и инициализированные)
                            return symbol != null && symbol.IsFinal && symbol.IsInitialized;
                        }
                    }
                }
            }

            return false;
        }

        private bool IsInfixExpressionConstant(JavaGrammarParser.InfixExpressionContext context)
        {
            if (context == null) return false;

            bool allOperandsConstant = true;

            // Проверяем все операнды в инфиксном выражении
            foreach (var expr2 in context.expression2())
            {
                if (!IsExpression2Constant(expr2))
                {
                    allOperandsConstant = false;
                    break;
                }
            }

            return allOperandsConstant;
        }

        // Анализ объявления импорта
        public override object VisitImportDeclaration(JavaGrammarParser.ImportDeclarationContext context)
        {
            bool isStatic = context.STATIC() != null;
            var qualifiedIdentifier = context.qualifiedIdentifier();
            bool isWildcard = false;

            string importedName = "";

            if (qualifiedIdentifier != null)
            {
                importedName = GetQualifiedIdentifierName(qualifiedIdentifier);

                // Проверяем, является ли последний компонент wildcard (*)
                if (context.DOT() != null && context.GetChild(context.ChildCount - 2).GetText() == "*")
                {
                    isWildcard = true;
                }
            }

            // Проверяем корректность статического импорта
            if (isStatic && !isWildcard)
            {
                // Статический импорт должен указывать на член класса (поле, метод)
                if (!importedName.Contains(".")) // Проверяем, есть ли разделение класса и члена
                {
                    ReportError($"Static import must specify a member: {importedName}", context);
                }
                else
                {
                    // Проверяем существование класса и его члена
                    string className = importedName.Substring(0, importedName.LastIndexOf('.'));
                    string memberName = importedName.Substring(importedName.LastIndexOf('.') + 1);

                    if (!_symbolTable.IsClass(className))
                    {
                        ReportError($"Class not found for static import: {className}", context);
                    }
                    else if (!_symbolTable.HasClassMember(className, memberName))
                    {
                        ReportError($"Member not found in class for static import: {className}.{memberName}", context);
                    }
                }
            }
            else if (!isStatic)
            {
                // Обычный импорт должен указывать на класс или пакет
                if (isWildcard)
                {
                    // Импорт всех классов из пакета
                    string packageName = importedName;
                    if (!_symbolTable.HasPackage(packageName))
                    {
                        ReportError($"Package not found: {packageName}", context);
                    }
                }
                else
                {
                    // Импорт конкретного класса
                    if (!_symbolTable.IsClass(importedName))
                    {
                        ReportError($"Class not found: {importedName}", context);
                    }
                }
            }

            // Проверяем конфликты импортов
            CheckImportConflicts(importedName, isStatic, isWildcard, context);

            return base.VisitImportDeclaration(context);
        }

        // Анализ квалифицированного идентификатора
        public override object VisitQualifiedIdentifier(JavaGrammarParser.QualifiedIdentifierContext context)
        {
            string qualifiedName = GetQualifiedIdentifierName(context);
            string lastPart = qualifiedName;

            // Извлекаем последнюю часть идентификатора (имя класса или члена)
            if (qualifiedName.Contains("."))
            {
                lastPart = qualifiedName.Substring(qualifiedName.LastIndexOf('.') + 1);
            }

            // Проверяем существование пакета/класса
            if (!qualifiedName.Contains(".") || _symbolTable.HasPackage(qualifiedName))
            {
                // Это пакет или простое имя
                if (!_symbolTable.HasPackage(qualifiedName))
                {
                    ReportError($"Package not found: {qualifiedName}", context);
                }
            }
            else
            {
                // Это класс в пакете
                string className = qualifiedName;
                string packageName = qualifiedName.Substring(0, qualifiedName.LastIndexOf('.'));

                if (!_symbolTable.HasPackage(packageName))
                {
                    ReportError($"Package not found: {packageName}", context);
                }
                else if (!_symbolTable.IsClass(className))
                {
                    ReportError($"Class not found: {className}", context);
                }
            }

            return base.VisitQualifiedIdentifier(context);
        }

        // Вспомогательные методы

        private string GetQualifiedIdentifierName(JavaGrammarParser.QualifiedIdentifierContext context)
        {
            if (context == null) return "";

            List<string> parts = new List<string>();
            foreach (var identifier in context.identifier())
            {
                if (identifier != null)
                {
                    parts.Add(identifier.GetText());
                }
            }

            return string.Join(".", parts);
        }

        private void CheckImportConflicts(string importedName, bool isStatic, bool isWildcard, ParserRuleContext context)
        {
            // Простая проверка: нельзя импортировать один и тот же класс дважды
            if (_symbolTable.IsImported(importedName))
            {
                ReportError($"Duplicate import: {importedName}", context);
            }
            else
            {
                // Добавляем импорт в таблицу для дальнейшей проверки конфликтов
                _symbolTable.AddImport(importedName, isStatic, isWildcard);

                // Проверяем конфликты с уже объявленными классами в текущем пакете
                if (!isStatic && !isWildcard && _symbolTable.HasClassInCurrentPackage(importedName))
                {
                    ReportError($"Import conflicts with locally defined class: {importedName}", context);
                }

                // Проверяем конфликты между wildcard-импортами
                if (isWildcard)
                {
                    var conflictingImports = _symbolTable.GetConflictingWildcards(importedName);
                    if (conflictingImports.Count > 0)
                    {
                        foreach (var conflict in conflictingImports)
                        {
                            ReportError($"Wildcard import conflicts with another wildcard import: {importedName} and {conflict}", context);
                        }
                    }
                }
            }
        }

        // Анализ приведения типов (Type)expression
        public override object VisitCastExpression(JavaGrammarParser.CastExpressionContext context)
        {
            // Получаем тип, к которому выполняется приведение
            string castType = GetCastTypeName(context);
            if (string.IsNullOrEmpty(castType))
            {
                ReportError("Invalid cast type", context);
                return base.VisitCastExpression(context);
            }

            // Получаем выражение, которое приводится
            var expression = context.expression();
            if (expression == null)
            {
                ReportError("Missing expression in cast operation", context);
                return base.VisitCastExpression(context);
            }

            string exprType = GetExpressionType(expression);

            // Проверяем совместимость типов для приведения
            if (!CanCastTypes(exprType, castType))
            {
                ReportError($"Cannot cast from {exprType} to {castType}", context);
            }

            // Дополнительные проверки для примитивных типов
            if (IsPrimitiveType(castType) && IsPrimitiveType(exprType))
            {
                CheckPrimitiveCastCompatibility(exprType, castType, context);
            }

            return base.VisitCastExpression(context);
        }

        // Анализ выражений в скобках (expression)
        public override object VisitParExpression(JavaGrammarParser.ParExpressionContext context)
        {
            // Просто посещаем вложенное выражение для семантической проверки
            var expression = context.expression();
            if (expression != null)
            {
                Visit(expression);
            }

            return base.VisitParExpression(context);
        }

        // Анализ литералов (числа, строки, булевы значения и т.д.)
        public override object VisitLiteral(JavaGrammarParser.LiteralContext context)
        {
            string literalValue = context.GetText();
            string literalType = GetLiteralType(context);

            // Проверки для числовых литералов
            if (IsNumericType(literalType))
            {
                CheckNumericLiteralRange(literalValue, literalType, context);
            }

            // Проверки для строковых литералов
            if (literalType == "String")
            {
                CheckStringLiteral(literalValue, context);
            }

            // Проверки для символьных литералов
            if (literalType == "char")
            {
                CheckCharLiteral(literalValue, context);
            }

            return base.VisitLiteral(context);
        }

        // Вспомогательные методы

        private string GetCastTypeName(JavaGrammarParser.CastExpressionContext context)
        {
            if (context.type() != null)
            {
                return GetTypeName(context.type());
            }
            return null;
        }

        private bool CanCastTypes(string fromType, string toType)
        {
            if (fromType == null || toType == null) return false;

            fromType = fromType.ToLower();
            toType = toType.ToLower();

            // Приведение к Object всегда допустимо
            if (toType == "object") return true;

            // Приведение к тому же типу всегда допустимо
            if (fromType == toType) return true;

            // Проверка для примитивных типов
            if (IsPrimitiveType(fromType) && IsPrimitiveType(toType))
            {
                return IsCompatiblePrimitiveCast(fromType, toType);
            }

            // Проверка для ссылочных типов (упрощенная)
            if (!IsPrimitiveType(fromType) && !IsPrimitiveType(toType))
            {
                // В реальном анализаторе здесь должна быть проверка иерархии наследования
                return true; // Временно разрешаем все приведения для ссылочных типов
            }

            // Приведение примитива к ссылочному типу (автоматическая упаковка)
            if (IsPrimitiveType(fromType) && !IsPrimitiveType(toType))
            {
                return GetWrapperType(fromType) == toType;
            }

            // Приведение ссылочного типа к примитиву (автоматическая распаковка)
            if (!IsPrimitiveType(fromType) && IsPrimitiveType(toType))
            {
                return fromType == GetWrapperType(toType);
            }

            return false;
        }

        private void CheckPrimitiveCastCompatibility(string fromType, string toType, ParserRuleContext context)
        {
            fromType = fromType.ToLower();
            toType = toType.ToLower();

            // Проверка потери точности при приведении
            if ((fromType == "double" || fromType == "float") &&
                (toType == "int" || toType == "long" || toType == "short" || toType == "byte"))
            {
                ReportWarning($"Possible loss of precision casting from {fromType} to {toType}", context);
            }

            // Проверка приведения boolean к другим типам
            if (fromType == "boolean" && toType != "boolean")
            {
                ReportError($"Cannot cast boolean to {toType}", context);
            }

            // Проверка приведения к boolean
            if (toType == "boolean" && fromType != "boolean")
            {
                ReportError($"Cannot cast {fromType} to boolean", context);
            }
        }

        private string GetLiteralType(JavaGrammarParser.LiteralContext context)
        {
            if (context.INTEGER_LITERAL() != null) return "int";
            if (context.FLOATING_POINT_LITERAL() != null) return "double";
            if (context.CHARACTER_LITERAL() != null) return "char";
            if (context.STRING_LITERAL() != null) return "String";
            if (context.TRUE() != null || context.FALSE() != null) return "boolean";
            if (context.NULL() != null) return "null";
            return "unknown";
        }

        private void CheckNumericLiteralRange(string value, string type, ParserRuleContext context)
        {
            try
            {
                // Удаляем суффиксы (l, L, f, F, d, D)
                string cleanValue = value.TrimEnd('l', 'L', 'f', 'F', 'd', 'D');

                switch (type)
                {
                    case "int":
                        int.Parse(cleanValue);
                        break;
                    case "long":
                        long.Parse(cleanValue);
                        break;
                    case "float":
                        float.Parse(cleanValue);
                        break;
                    case "double":
                        double.Parse(cleanValue);
                        break;
                }
            }
            catch (OverflowException)
            {
                ReportError($"Numeric literal out of range for type {type}: {value}", context);
            }
            catch (FormatException)
            {
                ReportError($"Invalid format for numeric literal of type {type}: {value}", context);
            }
        }

        private void CheckStringLiteral(string value, ParserRuleContext context)
        {
            // Проверяем экранирование символов
            if (value.Contains("\n") || value.Contains("\r"))
            {
                ReportError("String literals cannot contain unescaped newline characters", context);
            }

            // Проверяем закрывающую кавычку
            if (!value.EndsWith("\""))
            {
                ReportError("Unclosed string literal", context);
            }
        }

        private void CheckCharLiteral(string value, ParserRuleContext context)
        {
            // Проверяем формат символьного литерала
            if (!value.StartsWith("'") || !value.EndsWith("'"))
            {
                ReportError("Invalid character literal format", context);
                return;
            }

            string content = value.Substring(1, value.Length - 2);

            // Допустимые длины: 1 символ или экранированная последовательность
            if (content.Length != 1 && !content.StartsWith("\\"))
            {
                ReportError($"Invalid character literal: {value}. Must contain exactly one character.", context);
            }
        }

        private bool IsPrimitiveType(string type)
        {
            if (type == null) return false;

            type = type.ToLower();
            return type == "int" || type == "long" || type == "float" || type == "double" ||
                   type == "boolean" || type == "char" || type == "byte" || type == "short";
        }

        private bool IsCompatiblePrimitiveCast(string fromType, string toType)
        {
            // Матрица допустимых приведений для примитивных типов
            var compatibleCasts = new Dictionary<string, string[]>
            {
                ["byte"] = new[] { "short", "int", "long", "float", "double" },
                ["short"] = new[] { "int", "long", "float", "double" },
                ["char"] = new[] { "int", "long", "float", "double" },
                ["int"] = new[] { "long", "float", "double" },
                ["long"] = new[] { "float", "double" },
                ["float"] = new[] { "double" },
                ["boolean"] = new string[0] // Нельзя приводить boolean к другим примитивам
            };

            if (compatibleCasts.TryGetValue(fromType, out var allowedTypes))
            {
                return allowedTypes.Contains(toType);
            }

            return false;
        }

        private string GetWrapperType(string primitiveType)
        {
            primitiveType = primitiveType.ToLower();
            switch (primitiveType)
            {
                case "int": return "Integer";
                case "long": return "Long";
                case "float": return "Float";
                case "double": return "Double";
                case "boolean": return "Boolean";
                case "char": return "Character";
                case "byte": return "Byte";
                case "short": return "Short";
                default: return primitiveType;
            }
        }
    }
}
