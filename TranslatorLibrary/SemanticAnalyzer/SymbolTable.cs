namespace TranslatorLibrary.SemanticAnalyzer
{
    // Класс таблицы символов для управления областями видимости
    public class SymbolTable
    {
        private readonly Stack<Dictionary<string, SymbolInfo>> _scopes = new Stack<Dictionary<string, SymbolInfo>>();
        private readonly HashSet<string> _classes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Stack<string> _scopeNames = new Stack<string>();

        public void DeclareClass(string className)
        {
            _classes.Add(className);
        }
        public bool IsClass(string className)
        {
            // Базовые стандартные классы, необходимые для нашего подмножества
            HashSet<string> standardClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "String", "Object", "System", "Math", "Collections",
                "ArrayList", "HashSet", "HashMap", "List", "Set", "Map"
            };

            return standardClasses.Contains(className) ||
                   _classes.Contains(className);
        }
        public void DeclareVariable(string name, string type, bool isFinal = false)
        {
            if (IsDeclaredInCurrentScope(name))
                throw new SemanticException($"Variable '{name}' is already declared");

            _scopes.Peek()[name] = new SymbolInfo
            {
                Name = name,
                Type = type,
                IsFinal = isFinal,
                IsInitialized = false
            };
        }

        public bool IsVariableDeclared(string name)
        {
            foreach (var scope in _scopes)
            {
                if (scope.ContainsKey(name)) return true;
            }
            return false;
        }

        public SymbolInfo GetVariableInfo(string name)
        {
            foreach (var scope in _scopes)
            {
                if (scope.TryGetValue(name, out var symbol)) return symbol;
            }
            return null;
        }

        public bool IsDeclaredInCurrentScope(string name)
        {
            return _scopes.Peek().ContainsKey(name);
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
                throw new SemanticException($"Variable '{name}' is already declared in current scope");
            }

            var symbol = new SymbolInfo
            {
                Name = name,
                Type = type,
                IsFinal = isFinal,
                IsStatic = isStatic,
                IsInitialized = false
            };

            _scopes.Peek()[name] = symbol;
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
