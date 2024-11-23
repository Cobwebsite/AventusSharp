using AventusSharp.Data;
using AventusSharp.Tools.Attributes;
using CSharpToTypescript.Container;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        public static string GetFullName(ISymbol type)
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
            if (type == null) return null;

            string fullName = "";
            string typeName = type.ContainingNamespace.ToString() + "." + type.Name;
            if (type.IsGenericType)
            {
                typeName += "`" + type.TypeParameters.Length;
            }
            fullName += typeName + ", " + type.ContainingAssembly.Name;
            Type? realType = Type.GetType(fullName);
            if (realType == null)
            {
                Assembly? assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == type.ContainingAssembly.Name);
                if (assembly == null)
                {
                    try
                    {
                        assembly = Assembly.LoadFrom(ProjectManager.Config.outputDir + Path.DirectorySeparatorChar + type.ContainingAssembly.Name + ".dll");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                if (assembly != null)
                {
                    realType = assembly.GetType(typeName);
                }
            }

            if (type.IsGenericType && realType != null)
            {
                bool fullGeneric = false;
                List<Type> subTypes = new List<Type>();
                foreach (ITypeSymbol genericType in type.TypeArguments)
                {
                    if (genericType is INamedTypeSymbol named)
                    {
                        Type? subType = GetCompiledType(named);
                        if (subType == null)
                        {
                            throw new Exception("impossible");
                        }
                        subTypes.Add(subType);
                    }
                    else if (genericType is ITypeParameterSymbol typeParameter)
                    {
                        fullGeneric = true;
                    }
                    else
                    {
                        throw new Exception("impossible");
                    }
                }
                if (!fullGeneric)
                {
                    realType = realType.MakeGenericType(subTypes.ToArray());
                }
            }

            return realType;
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
