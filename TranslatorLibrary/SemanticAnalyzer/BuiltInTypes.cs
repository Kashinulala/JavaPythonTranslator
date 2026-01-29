namespace TranslatorLibrary.SemanticAnalyzer
{
    public static class BuiltInTypes
    {
        private static readonly Dictionary<string, TypeInfo> _types = new Dictionary<string, TypeInfo>();
        private static HashSet<int> a = [];
        static BuiltInTypes()
        {
            // Инициализируем информацию о стандартных типах
            InitializeBuiltInTypes();
        }

        private static void InitializeBuiltInTypes()
        {
            // Пример: класс System
            var systemType = new TypeInfo();
            systemType.Fields.Add("out", new FieldInfo("PrintStream"));
            systemType.Fields.Add("in", new FieldInfo("InputStream"));
            _types.Add("System", systemType);

            // Пример: класс PrintStream
            var printStreamType = new TypeInfo();

            if (!printStreamType.Methods.ContainsKey("println"))
            {
                printStreamType.Methods.Add("println", new MethodInfo("void", "Object"));
            }
            if (!printStreamType.Methods.ContainsKey("print"))
            {
                printStreamType.Methods.Add("print", new MethodInfo("void", "Object"));
            }
            _types.Add("PrintStream", printStreamType);

            // Пример: класс String
            var stringType = new TypeInfo();
            if (!stringType.Methods.ContainsKey("length"))
            {
                stringType.Methods.Add("length", new MethodInfo("int"));
            }
            if (!stringType.Methods.ContainsKey("substring"))
            {
                stringType.Methods.Add("substring", new MethodInfo("String", "int", "int"));
            }
            if (!stringType.Methods.ContainsKey("equals"))
            {
                stringType.Methods.Add("equals", new MethodInfo("boolean", "Object"));
            }
            _types.Add("String", stringType);

            // Пример: класс Object
            var objectType = new TypeInfo();
            if (!objectType.Methods.ContainsKey("toString"))
            {
                objectType.Methods.Add("toString", new MethodInfo("String"));
            }
            if (!objectType.Methods.ContainsKey("equals"))
            {
                objectType.Methods.Add("equals", new MethodInfo("boolean", "Object"));
            }
            if (!objectType.Methods.ContainsKey("hashCode"))
            {
                objectType.Methods.Add("hashCode", new MethodInfo("int"));
            }
            _types.Add("Object", objectType);
        }

        // Проверить, является ли тип встроенным
        public static bool IsBuiltInType(string typeName)
        {
            return _types.ContainsKey(typeName);
        }

        // Получить информацию о типе
        public static TypeInfo GetTypeInfo(string typeName)
        {
            _types.TryGetValue(typeName, out TypeInfo info);
            return info;
        }

        // Проверить, существует ли поле в типе
        public static bool HasField(string typeName, string fieldName)
        {
            var typeInfo = GetTypeInfo(typeName);
            return typeInfo != null && typeInfo.Fields.ContainsKey(fieldName);
        }
        public static string GetFieldType(string typeName, string fieldName)
        {
            var typeInfo = GetTypeInfo(typeName);
            if (typeInfo != null && typeInfo.Fields.TryGetValue(fieldName, out FieldInfo fieldInfo))
            {
                return fieldInfo.Type;
            }
            return null; // Поле не найдено
        }
        public static bool HasMethod(string typeName, string methodName)
        {
            var typeInfo = GetTypeInfo(typeName);
            return typeInfo != null && typeInfo.Methods.ContainsKey(methodName);
        }

        public static MethodInfo GetMethodInfo(string typeName, string methodName)
        {
            var typeInfo = GetTypeInfo(typeName);
            if (typeInfo != null && typeInfo.Methods.TryGetValue(methodName, out MethodInfo methodInfo))
            {
                return methodInfo;
            }
            return null; // Метод не найдено
        }
    }

    // Простая структура для хранения информации о методе
    public class MethodInfo
    {
        public string ReturnType { get; set; }
        public List<string> ParameterTypes { get; set; } = new List<string>();

        public MethodInfo(string returnType, params string[] paramTypes)
        {
            ReturnType = returnType;
            ParameterTypes.AddRange(paramTypes);
        }
    }

    // Простая структура для хранения информации о поле
    public class FieldInfo
    {
        public string Type { get; set; }

        public FieldInfo(string type)
        {
            Type = type;
        }
    }

    public class TypeInfo
    {
        public Dictionary<string, FieldInfo> Fields { get; set; } = new Dictionary<string, FieldInfo>();
        public Dictionary<string, MethodInfo> Methods { get; set; } = new Dictionary<string, MethodInfo>();
    }
}
