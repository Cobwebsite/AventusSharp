using AventusSharp.Tools;
using AventusSharp.Tools.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpToTypescript.Container
{
    public class WithErrorContainer : BaseClassContainer
    {
        private bool isPasserel = false;
        private bool isVoid = false;
        public static bool Is(INamedTypeSymbol type, string fileName, out BaseContainer? result)
        {
            result = null;
            if (Tools.Is<IWithError>(type))
            {
                if (Tools.ExportToTypesript(type, ProjectManager.Config.exportErrorsByDefault))
                {
                    result = new WithErrorContainer(type);
                }
                return true;
            }
            return false;
        }

        public WithErrorContainer(INamedTypeSymbol type) : base(type)
        {
            if (Tools.GetFullName(this.type) == typeof(VoidWithError).FullName)
            {
                isPasserel = true;
                isVoid = true;
            }
            else if (Tools.GetFullName(this.type) == typeof(ResultWithError<>).FullName?.Split("`")[0])
            {
                isPasserel = true;
            }
        }


        protected override bool IsValidExtendsClass(INamedTypeSymbol type)
        {
            return isPasserel == false;
        }
        protected override bool IsValidField(IFieldSymbol type)
        {
            return isPasserel == false;
        }

        protected override bool IsValidProperty(IPropertySymbol type)
        {
            return isPasserel == false;
        }

        protected override void AddExtends(Action<string> add)
        {
            if (isPasserel)
            {
                if (isVoid)
                {
                    add("Aventus.VoidWithError<T>");
                }
                else
                {
                    add("Aventus.ResultWithError<T, U>");
                }
            }
        }

        protected override void DefineFullname(List<string> result)
        {
            if (IsConvertible)
            {
                string typeName = "\"" + type.ContainingNamespace.ToString() + "." + type.Name + ", " + type.ContainingAssembly.Name + "\"";
                if (isPasserel)
                {
                    AddTxt("public static get Fullname(): string { return " + typeName + "; }", result);
                    return;
                }
                Type? realType = Tools.GetCompiledType(type.BaseType);
                if (realType != null && !realType.IsInterface && !realType.IsAbstract)
                {
                    AddTxt("public static override get Fullname(): string { return " + typeName + "; }", result);
                }
                else
                {
                    AddTxt("public static get Fullname(): string { return " + typeName + "; }", result);
                }
            }
        }

        protected override string? CustomReplacer(ISymbol? type, string fullname, string? result)
        {
            return applyReplacer(ProjectManager.Config.replacer.withError, fullname, result);
        }

    }
}
