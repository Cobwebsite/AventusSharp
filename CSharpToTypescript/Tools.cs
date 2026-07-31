using AventusSharp.Data;
using AventusSharp.Routes;
using AventusSharp.Tools.Attributes;
using AventusSharp.WebSocket;
using CSharpToTypescript.Container;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CSharpToTypescript
{
    internal static class Tools
    {
        public static bool ExportToTypesript(INamedTypeSymbol type, bool defaultValue)
        {
            List<AttributeData> attrs = type.GetAttributes().ToList();
            if (defaultValue)
            {
                if (attrs.Find(p => p.AttributeClass != null && p.AttributeClass.ToString() == typeof(NoExport).FullName) != null)
                {
                    return false;
                }
            }
            else
            {
                if (attrs.Find(p => p.AttributeClass != null && p.AttributeClass.ToString() == typeof(Export).FullName) != null)
                {
                    return true;
                }
            }

            return defaultValue;
        }


        public static bool HasAttribute<X>(ISymbol type)
        {
            List<AttributeData> attrs = type.GetAttributes().ToList();
            if (attrs.Find(p => p.AttributeClass != null && p.AttributeClass.ToString() == typeof(X).FullName) != null)
            {
                return true;
            }
            return false;
        }

        public static bool IsSameType<X>(INamedTypeSymbol type)
        {
            return type.ToString() == typeof(X).FullName;
        }

        public static string GetRelativePath(string currentPathTxt, string importPathTxt)
        {
            List<string> currentPath = currentPathTxt.Split(Path.DirectorySeparatorChar).ToList();
            // start from the directory not the file
            currentPath.Remove(currentPath.Last());
            List<string> importPath = importPathTxt.Split(Path.DirectorySeparatorChar).ToList();

            for (int i = 0; i < currentPath.Count; i++)
            {
                if (importPath.Count > i)
                {
                    if (currentPath[i] == importPath[i])
                    {
                        currentPath.RemoveAt(i);
                        importPath.RemoveAt(i);
                        i--;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            string finalPathToImport = "";
            for (int i = 0; i < currentPath.Count; i++)
            {
                finalPathToImport += "../";
            }
            if (finalPathToImport == "")
            {
                finalPathToImport += "./";
            }
            finalPathToImport += string.Join("/", importPath);
            return finalPathToImport;
        }

        public static string GetFullName(this ISymbol type)
        {
            List<string> parentNames = new List<string>();
            ISymbol t = type;
            while (t.ContainingType != null)
            {
                parentNames.Add(t.ContainingType.Name);
                t = t.ContainingType;
            }
            if (parentNames.Count > 0)
            {
                return type.ContainingNamespace.ToString() + "." + string.Join("+", parentNames) + "+" + type.Name;
            }
            return type.ContainingNamespace.ToString() + "." + type.Name;
        }

        public static bool Is<X>(INamedTypeSymbol type, bool avoidInterface = false, bool avoidBaseAssembly = false)
        {
            bool result;
            if (!avoidInterface)
            {
                result = type.AllInterfaces.ToList().Find(p => IsSameType<X>(p)) != null || Tools.GetFullName(type) == typeof(X).FullName;
            }
            else
            {
                result = type.AllInterfaces.ToList().Find(p => IsSameType<X>(p)) != null;
            }
            if (!result)
            {
                return false;
            }
            if (avoidBaseAssembly)
            {
                string fullName = type.ContainingNamespace.ToString() + "." + type.Name;
                if (type.IsGenericType)
                {
                    fullName += "`" + type.TypeParameters.Length;
                }
                Type? realType = GetTypeFromFullName(fullName);
                if (realType != null && realType.Assembly == typeof(IStorable).Assembly)
                {
                    return false;
                }
            }
            return true;
        }
        public static Type? GetCompiledType(INamedTypeSymbol? type)
        {
            if (type == null)
                return null;

            string metadataName = GetMetadataName(type);

            Assembly? assembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(a =>
                    string.Equals(
                        a.GetName().Name,
                        type.ContainingAssembly.Name,
                        StringComparison.Ordinal
                    )
                );

            if (assembly == null)
            {
                try
                {
                    string assemblyPath = Path.Combine(
                        ProjectManager.Config.outputDir,
                        type.ContainingAssembly.Name + ".dll"
                    );

                    assembly = Assembly.LoadFrom(assemblyPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return null;
                }
            }

            Type? realType = assembly.GetType(
                metadataName,
                throwOnError: false,
                ignoreCase: false
            );

            if (realType == null)
                return null;

            if (!type.IsGenericType)
                return realType;

            var genericArguments = new List<Type>();

            foreach (ITypeSymbol typeArgument in type.TypeArguments)
            {
                // Le type contient encore un paramètre générique ouvert.
                if (typeArgument is ITypeParameterSymbol)
                    return realType;

                if (typeArgument is not INamedTypeSymbol namedArgument)
                    return null;

                Type? compiledArgument = GetCompiledType(namedArgument);

                if (compiledArgument == null)
                    return null;

                genericArguments.Add(compiledArgument);
            }

            if (realType.IsGenericTypeDefinition)
            {
                return realType.MakeGenericType(genericArguments.ToArray());
            }

            return realType;
        }
        private static string GetMetadataName(INamedTypeSymbol type)
        {
            var containingTypes = new Stack<string>();

            INamedTypeSymbol? current = type;

            while (current != null)
            {
                containingTypes.Push(current.MetadataName);
                current = current.ContainingType;
            }

            string nestedTypeName = string.Join("+", containingTypes);

            if (type.ContainingNamespace is { IsGlobalNamespace: false } ns)
            {
                return ns.ToDisplayString() + "." + nestedTypeName;
            }

            return nestedTypeName;
        }

        public static ITypeSymbol GetTypeSymbol(Type type)
        {
            string fullName = string.IsNullOrEmpty(type.Namespace) ? type.Name : type.Namespace + "." + type.Name;
            ITypeSymbol? typeSymbol = ProjectManager.Compilation.GetTypeByMetadataName(fullName ?? "");


            if (typeSymbol == null)
            {
                throw new Exception("impossbile");
            }
            if (typeSymbol is INamedTypeSymbol namedType)
            {
                if (type.IsGenericType && !type.IsGenericTypeDefinition)
                {
                    List<ITypeSymbol> types = new List<ITypeSymbol>();
                    Type[] typeGenerics = type.GetGenericArguments();
                    for (int i = 0; i < typeGenerics.Length; i++)
                    {
                        Type typeGeneric = typeGenerics[i];
                        if (typeGeneric.IsGenericTypeParameter)
                        {
                            types.Add(namedType.TypeParameters[i]);
                        }
                        else
                        {
                            types.Add(GetTypeSymbol(typeGeneric));
                        }
                    }



                    typeSymbol = namedType.Construct(types.ToArray());
                }
            }
            return typeSymbol;
        }
        public static INamedTypeSymbol GetNameTypeSymbol(Type type)
        {
            ITypeSymbol result = GetTypeSymbol(type);
            if (result is INamedTypeSymbol named)
            {
                return named;
            }
            throw new Exception("impossbile");
        }

        public static MethodInfo? GetMethodInfo(IMethodSymbol methodSymbol, Type @class)
        {
            List<MethodInfo> methods = @class.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).ToList();
            foreach (MethodInfo method in methods)
            {
                if (method.Name == methodSymbol.Name)
                {
                    ParameterInfo[] methodParams = method.GetParameters();
                    if (methodParams.Length == methodSymbol.Parameters.Length)
                    {
                        bool allSame = true;
                        for (int i = 0; i < methodParams.Length; i++)
                        {
                            Type paramType = methodParams[i].ParameterType;
                            if (!paramType.Compare(methodSymbol.Parameters[i].Type))
                            {
                                allSame = false;
                                break;
                            }
                        }
                        if (allSame)
                        {
                            return method;
                        }
                    }
                }
            }
            throw new Exception("impossible to load the method " + methodSymbol.Name + " from " + @class.Name);
        }
        public static MethodInfo? GetMethodInfo(string methodName, List<string> @params, Type @class)
        {
            List<MethodInfo> methods = @class.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).ToList();
            foreach (MethodInfo method in methods)
            {
                if (method.Name == methodName)
                {
                    ParameterInfo[] methodParams = method.GetParameters();
                    if (methodParams.Length == @params.Count)
                    {
                        bool allSame = true;
                        for (int i = 0; i < methodParams.Length; i++)
                        {
                            Type paramType = methodParams[i].ParameterType;
                            if (paramType.FullName!.ToString() != @params[i])
                            {
                                allSame = false;
                                break;
                            }
                        }
                        if (allSame)
                        {
                            return method;
                        }
                    }
                }
            }
            throw new Exception("impossible to load the method " + methodName + " from " + @class.Name);
        }
        public static MethodInfo? GetMethodInfo(RouteExposeHttp methodSymbol, Type @class)
        {
            return GetMethodInfo(methodSymbol.MethodName, methodSymbol.Params, @class);
        }
        public static MethodInfo? GetMethodInfo(WsExpose methodSymbol, Type @class)
        {
            return GetMethodInfo(methodSymbol.MethodName, methodSymbol.Params, @class);
        }

        public static MemberInfo? GetMemberInfo(ISymbol memberSymbol, Type @class)
        {
            if (memberSymbol is IPropertySymbol propertySymbol) return GetPropertyInfo(propertySymbol, @class);
            if (memberSymbol is IFieldSymbol fieldSymbol) return GetFieldInfo(fieldSymbol, @class);
            return null;
        }
        public static PropertyInfo GetPropertyInfo(IPropertySymbol memberSymbol, Type @class)
        {
            List<PropertyInfo> members = @class.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).ToList();
            foreach (PropertyInfo member in members)
            {
                if (member.Name == memberSymbol.Name)
                {
                    return member;
                }
            }
            throw new Exception("impossible to load the property " + memberSymbol.Name + " from " + @class.Name);
        }
        public static FieldInfo GetFieldInfo(IFieldSymbol memberSymbol, Type @class)
        {
            List<FieldInfo> members = @class.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).ToList();
            foreach (FieldInfo member in members)
            {
                if (member.Name == memberSymbol.Name)
                {
                    return member;
                }
            }
            throw new Exception("impossible to load the field " + memberSymbol.Name + " from " + @class.Name);
        }

        public static bool IsSubclass(Type parent, Type child)
        {
            Type? casted;
            return IsSubclass(parent, child, out casted);
        }

        public static bool IsSubclass(Type parent, Type child, out Type? castedParent)
        {
            castedParent = null;
            Type? typeLoop = child;
            while (typeLoop != null && typeLoop != typeof(object))
            {
                var cur = typeLoop.IsGenericType ? typeLoop.GetGenericTypeDefinition() : typeLoop;
                if (parent == cur)
                {
                    castedParent = typeLoop;
                    return true;
                }
                typeLoop = typeLoop.BaseType;
            }
            return false;
        }

        public static Type? GetTypeFromFullName(string fullName)
        {
            Type? realType = ProjectManager.Config.compiledAssembly?.GetType(fullName);
            if (realType == null)
            {
                foreach (Assembly ass in AppDomain.CurrentDomain.GetAssemblies())
                {
                    realType = ass.GetType(fullName);
                    if (realType != null)
                    {
                        return realType;
                    }
                }
                throw new Exception("something went wrong");
            }
            return realType;
        }


        public static string WriteAsSymbol(this Type type)
        {
            string name = "";
            if (type.IsGenericParameter || string.IsNullOrEmpty(type.Namespace))
            {
                name = type.Name;
            }
            else
            {
                name = type.Namespace + "." + type.Name;
            }
            if (type.IsGenericType)
            {
                name = name.Split("`")[0];
                List<string> generics = new();
                foreach (Type genericType in type.GetGenericArguments())
                {
                    generics.Add(WriteAsSymbol(genericType));
                }
                name += "<" + string.Join(",", generics) + ">";
            }
            return name;
        }
        public static bool Compare(this Type type, ITypeSymbol symbol)
        {
            Func<ITypeSymbol, string> writeGeneric2 = (ITypeSymbol loopType) => "";

            writeGeneric2 = (ITypeSymbol loopType) =>
            {
                string name = "";
                name = loopType.ContainingNamespace.ToString() + "." + loopType.Name;
                if (loopType is INamedTypeSymbol namedType)
                {
                    if (namedType.IsGenericType)
                    {
                        List<string> generics = new();
                        foreach (ITypeSymbol typeTemp in namedType.TypeArguments)
                        {
                            generics.Add(writeGeneric2(typeTemp));
                        }
                        name += "<" + string.Join(",", generics) + ">";
                    }
                }
                else if (loopType is ITypeParameterSymbol)
                {
                    name = loopType.Name;
                }
                return name;
            };

            string fullName = type.WriteAsSymbol();
            string fullName2 = writeGeneric2(symbol);

            return fullName == fullName2;
        }

    }
}
