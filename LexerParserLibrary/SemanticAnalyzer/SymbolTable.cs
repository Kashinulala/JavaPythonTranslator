namespace LexerParserLibrary.SemanticAnalyzer
{
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
}
