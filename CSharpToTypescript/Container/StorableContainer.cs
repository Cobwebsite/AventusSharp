using AventusSharp.Data;
using AventusSharp.Tools.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CSharpToTypescript.Container
{
    internal class StorableContainer : BaseClassContainer
    {

        public static bool Is(INamedTypeSymbol type, string fileName, out BaseContainer? result)
        {
            result = null;
            if (type.AllInterfaces.ToList().Find(p => Tools.IsSameType<IStorable>(p)) != null)
            {
                if (Tools.ExportToTypesript(type, ProjectManager.Config.exportStorableByDefault))
                {
                    result = new StorableContainer(type);
                }
                return true;
            }
            return false;
        }

        public StorableContainer(INamedTypeSymbol type) : base(type) { }


        protected override void AddImplements(List<string> implements)
        {
            if (!isInterface && !implements.Contains("Aventus.IData"))
            {
                implements.Add("Aventus.IData");
            }
        }
        protected override void AddExtends(Action<string> add)
        {
            if (isInterface)
            {
                add("Aventus.IData");
            }
        }

        protected override void DefineFullname(List<string> result)
        {
            if (IsConvertible)
            {
                string typeName = "\"" + type.ContainingNamespace.ToString() + "." + type.Name + ", " + type.ContainingAssembly.Name + "\"";
                AddTxt("/** Fullname of the class */", result);
                AddTxt("public static override get Fullname(): string { return " + typeName + "; }", result);
            }
        }

        protected override string? CustomReplacer(ISymbol? type, string fullname, string? result)
        {
            return applyReplacer(ProjectManager.Config.replacer.storable, fullname, result);
        }

    }
}
