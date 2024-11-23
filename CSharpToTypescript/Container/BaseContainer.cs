using AventusSharp.Data;
using AventusSharp.Routes;
using AventusSharp.Routes.Request;
using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;
using AventusSharp.WebSocket;
using AventusSharp.WebSocket.Event;
using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis;
using MySqlX.XDevAPI.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace CSharpToTypescript.Container
{
    public enum IsContainerResult
    {
        TrueAndExport,
        True,
        False
    }
    public abstract class BaseContainer
    {
        public static bool enumToTypeof = false;
        public List<ISymbol> unresolved = new List<ISymbol>();
        public Dictionary<string, List<string>> importedFiles = new Dictionary<string, List<string>>();
        public string Namespace { get; private set; } = "";
        public INamedTypeSymbol type;
        public string Content { get; private set; } = "";

        public bool CanBeAdded { get; protected set; } = true;

        public bool IsConvertible { get; set; } = false;

        protected Dictionary<string, string?> defaultValueForGenerics = new Dictionary<string, string?>();

        public Type realType;

        public bool IsInternal { get; set; } = false;


        public BaseContainer(INamedTypeSymbol type)
        {
            this.type = type;

            var splitted = type.ContainingSymbol.ToString()?.Split(".").ToList();
            if (splitted != null)
            {
                splitted.RemoveAt(0);
                Namespace = string.Join(".", splitted);
            }
            string sep = ".";
            if (type.ContainingSymbol.ToString() != type.ContainingNamespace.ToString())
            {
                sep = "+";
            }
            string fullName = type.ContainingSymbol.ToString() + sep + type.Name;
            if (type.IsGenericType)
            {
                fullName += "`" + type.TypeParameters.Length;
            }
            Type? realType = Tools.GetTypeFromFullName(fullName);
            if (realType == null)
            {
                throw new Exception("something went wrong");
            }
            this.realType = realType;

            object? attr = this.realType.GetCustomAttributes(false).ToList().Find(p => p is Export);
            if (attr != null)
            {
                Export typescriptAttr = (Export)attr;
                if (typescriptAttr.Namespace != null)
                {
                    Namespace = typescriptAttr.Namespace;
                }
                if (typescriptAttr.Internal != null)
                {
                    IsInternal = (bool)typescriptAttr.Internal;
                }
            }
        }
        public void Write()
        {
            Content = WriteAction();
        }
        protected abstract string WriteAction();

        protected FileWriter fileWriter = new FileWriter();
        public string GetIndentedText(string txt)
        {
            return fileWriter.GetIndentedText(txt);
        }
        public void AddTxt(string txt, List<string> result)
        {
            fileWriter.AddTxt(txt, result);
        }
        public void AddTxt(List<string> txts, List<string> result)
        {
            fileWriter.AddTxt(txts, result);
        }
        public void AddTxtOpen(string txt, List<string> result)
        {
            fileWriter.AddTxtOpen(txt, result);
        }
        public void AddTxtClose(string txt, List<string> result)
        {
            fileWriter.AddTxtClose(txt, result);
        }

        public void AddIndent()
        {
            fileWriter.AddIndent();
        }
        public void RemoveIndent()
        {
            fileWriter.RemoveIndent();
        }

        public static string GetAccessibilityExport(ISymbol type)
        {
            return type.DeclaredAccessibility switch
            {
                Accessibility.Public => "export ",
                Accessibility.Protected => "export ",
                Accessibility.Friend => "export ",
                Accessibility.ProtectedAndFriend => "export ",
                Accessibility.ProtectedOrFriend => "export ",
                _ => ""
            };
        }
        public static string GetAccessibility(ISymbol type)
        {
            string result = type.DeclaredAccessibility switch
            {
                Accessibility.Public => "public ",
                Accessibility.Protected => "protected ",
                Accessibility.Private => "private ",
                Accessibility.Friend => "protected ",
                Accessibility.ProtectedAndFriend => "protected ",
                Accessibility.ProtectedOrFriend => "protected ",
                _ => ""
            };
            if (type.IsStatic)
            {
                result += "static ";
            }
            return result;
        }
        protected string GetDocumentation(ISymbol type)
        {
            List<string> result = new List<string>();
            string commentTxt = type.GetDocumentationCommentXml() ?? "";
            Match match = Regex.Match(commentTxt, @"<summary>([\s|\S]*)</summary>");
            if (match.Success)
            {
                string[] comments = match.Groups[1].Value.Trim().Split("\r\n");
                AddTxt("/**", result);
                foreach (string comment in comments)
                {
                    AddTxt(" * " + comment.Trim(), result);
                }

                AddTxt(" */", result);
            }
            return string.Join("\r\n", result);
        }

        public virtual string GetTypeName(Type type, int depth = 0, bool genericExtendsConstraint = false)
        {
            if (type.IsGenericTypeParameter)
            {
                return type.Name;
            }
            return GetTypeName(Tools.GetTypeSymbol(type), depth, genericExtendsConstraint);
        }

        public string GetAventusTypeName(string type)
        {
            string result = applyReplacer(ProjectManager.Config.replacer.all, "", type) ?? "";
            return CustomReplacer(null, "", result) ?? "";
        }
        public virtual string GetTypeName(ISymbol type, int depth = 0, bool genericExtendsConstraint = false)
        {
            string name = "";
            bool isNullable = false;
            if (type is ITypeSymbol typeSymbol && typeSymbol.NullableAnnotation == NullableAnnotation.Annotated)
            {
                isNullable = true;
                if (typeSymbol is INamedTypeSymbol named && named.TypeArguments.Length == 1)
                {
                    type = named.TypeArguments[0];
                }
            }
            if (type.ContainingAssembly.Name == ProjectManager.CurrentAssemblyName)
            {
                SyntaxTree? general = this.type.Locations[0].SourceTree;
                SyntaxTree? current = type.Locations[0].SourceTree;
                if (general != current)
                {
                    // need to load file
                    if (!unresolved.Contains(type) && Tools.GetFullName(type) != typeof(IStorable).FullName)
                    {
                        unresolved.Add(type);
                    }
                }

                name = type.Name;
            }
            else
            {
                name = type.ToString() ?? "";
            }

            bool isFull;
            name = GetVariantTypeName(type, depth, genericExtendsConstraint, name, out isFull);
            if (enumToTypeof && type is ITypeSymbol typeSymbol1 && typeSymbol1.TypeKind == TypeKind.Enum)
            {
                name = "typeof " + name;
            }
            if (!isFull && type is INamedTypeSymbol namedType && namedType.IsGenericType && namedType.Name != "Nullable")
            {
                name = name.Split("<")[0];
                name = DetermineGenericType(namedType, name, depth, genericExtendsConstraint);
            }
            //name = Replacer(name);
            if (isNullable && !name.EndsWith("?"))
            {
                name += "?";
            }
            return name;
        }
        protected string DetermineGenericType(INamedTypeSymbol type, string? name, int depth, bool genericExtendsConstraint)
        {
            // if empty name => mabye its a full replacement by the user
            if (name == "")
            {
                return name;
            }
            // it's an internal case for list and dico
            if (name == null)
            {
                name = "";
            }
            int i = 0;
            if (type.AllInterfaces.ToList().Find(p => Tools.IsSameType<IStorable>(p)) != null)
            {
                i = 1;
            }
            List<string> gens = new List<string>();
            for (; i < type.TypeArguments.Length; i++)
            {
                ITypeSymbol genericType = type.TypeArguments[i];
                gens.Add(ParseGenericType(genericType, depth, genericExtendsConstraint));
            }
            if (gens.Count > 0)
            {
                name += "<" + string.Join(", ", gens) + ">";
            }
            return name;
        }

        protected virtual string ParseGenericType(ITypeSymbol type, int depth, bool genericExtendsConstraint)
        {
            if (type is INamedTypeSymbol casted)
            {
                return GetTypeName(casted, depth + 1);
            }
            if (type is ITypeParameterSymbol typeParameter)
            {
                if (genericExtendsConstraint)
                {
                    List<string> extends = new List<string>();
                    foreach (ITypeSymbol genericConstraint in typeParameter.ConstraintTypes)
                    {
                        string extendsTemp = GetTypeName(genericConstraint, depth + 1);
                        if (extendsTemp != "")
                        {
                            extends.Add(extendsTemp);
                        }
                    }
                    string result = type.Name;
                    if (extends.Count > 0)
                    {
                        result += " extends " + string.Join(", ", extends);
                    }
                    if (defaultValueForGenerics.ContainsKey(type.Name) && !string.IsNullOrEmpty(defaultValueForGenerics[type.Name]))
                    {
                        result += " = " + defaultValueForGenerics[type.Name];
                    }
                    return result;
                }
            }

            return type.Name;
        }


        protected bool HasAttribute<X>(ISymbol type)
        {
            return type.GetAttributes().ToList()
                .Find(p => p.AttributeClass != null && p.AttributeClass.ToString() == typeof(X).FullName) != null;
        }


        public string GetVariantTypeName(ISymbol type, int depth, bool genericExtendsConstraint, string name, out bool isFull)
        {
            isFull = false;
            string fullName = Tools.GetFullName(type);
            bool isNullable = false;
            if (fullName == "System.Nullable" && type is INamedTypeSymbol namedTypeSymbol)
            {
                fullName = Tools.GetFullName(namedTypeSymbol.TypeArguments[0]);
                isNullable = true;
            }
            string result = name;
            if (fullName == typeof(int).FullName) result = "number";
            else if (fullName == typeof(double).FullName) result = "number";
            else if (fullName == typeof(long).FullName) result = "number";
            else if (fullName == typeof(float).FullName) result = "number";
            else if (fullName == typeof(decimal).FullName) result = "number";
            else if (fullName == typeof(bool).FullName) result = "boolean";
            else if (fullName == typeof(string).FullName) result = "string";
            else if (fullName == typeof(Enum).FullName) result = "Aventus.Enum";
            else if (fullName == typeof(DateTime).FullName) result = "Date";
            else if (fullName == typeof(IStorable).FullName) result = "Aventus.IData";
            else if (fullName == typeof(GenericError).FullName) result = "Aventus.GenericError";
            else if (fullName == typeof(Router).FullName) result = "Aventus.HttpRoute";
            else if (fullName == typeof(WsEvent<>).FullName?.Split("`")[0]) result = "AventusSharp.WebSocket.WsEvent";
            else if (fullName == typeof(WsRouter).FullName) result = "AventusSharp.WebSocket.Router";
            else if (fullName == typeof(WsEndPoint).FullName) result = "AventusSharp.WebSocket.EndPoint";
            else if (fullName == typeof(HttpFile).FullName) result = "File";
            else if (fullName == typeof(List<>).FullName?.Split("`")[0] && type is INamedTypeSymbol namedType)
            {
                isFull = true;
                result = DetermineGenericType(namedType, null, depth, genericExtendsConstraint);

                if (result.StartsWith("<"))
                {
                    result = result.Substring(1, result.Length - 2);
                }
                result += "[]";
            }
            else if (fullName == typeof(Dictionary<,>).FullName?.Split("`")[0] && type is INamedTypeSymbol namedTypeDico)
            {
                isFull = true;
                result = DetermineGenericType(namedTypeDico, null, depth, genericExtendsConstraint);
                result = "Map" + result;
            }
            if (isNullable)
            {
                fullName += "?";
            }
            result = applyReplacer(ProjectManager.Config.replacer.all, fullName, result) ?? "";
            return CustomReplacer(type, fullName, result) ?? "";
        }
        protected virtual string? CustomReplacer(ISymbol? type, string fullname, string? result)
        {
            return result;
        }

        public void addImport(string file, string toImport)
        {
            file = ProjectManager.Config.AbsoluteUrl(file);
            if (!importedFiles.ContainsKey(file))
            {
                importedFiles[file] = new();
            }
            if (!importedFiles[file].Contains(toImport))
            {
                importedFiles[file].Add(toImport);
            }
        }
        protected string? applyReplacer(ProjectConfigReplacerPart part, string fullname, string? result)
        {
            foreach (KeyValuePair<string, ProjectConfigReplacerInfo> info in part.type)
            {
                if (fullname != "" && info.Key == fullname)
                {
                    result = info.Value.result;
                    if (!string.IsNullOrEmpty(info.Value.file))
                    {
                        string file = ProjectManager.Config.AbsoluteUrl(info.Value.file);
                        if (!importedFiles.ContainsKey(file))
                        {
                            importedFiles[file] = new();
                        }
                        if (!importedFiles[file].Contains(result))
                        {
                            if (info.Value.useTypeImport)
                            {
                                importedFiles[file].Add("type " + result);
                            }
                            else
                            {
                                importedFiles[file].Add(result);
                            }
                        }
                    }
                    break;
                }
            }

            foreach (KeyValuePair<string, ProjectConfigReplacerInfo> info in part.result)
            {
                if (info.Key == result)
                {
                    result = info.Value.result;
                    if (!string.IsNullOrEmpty(info.Value.file))
                    {
                        string file = ProjectManager.Config.AbsoluteUrl(info.Value.file);
                        if (!importedFiles.ContainsKey(file))
                        {
                            importedFiles[file] = new();
                        }
                        if (!importedFiles[file].Contains(result))
                        {
                            if (info.Value.useTypeImport)
                            {
                                importedFiles[file].Add("type " + result);
                            }
                            else
                            {
                                importedFiles[file].Add(result);
                            }
                        }
                    }
                    break;
                }
            }

            return result;
        }
    }
}
