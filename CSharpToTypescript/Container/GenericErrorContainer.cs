using AventusSharp.Tools;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpToTypescript.Container
{
    public class GenericErrorContainer : BaseClassContainer
    {
        public static bool Is(INamedTypeSymbol type, string fileName, out BaseContainer? result)
        {
            result = null;
            if (Tools.Is<IGenericError>(type, true, false))
            {
                if (Tools.ExportToTypesript(type, ProjectManager.Config.exportErrorsByDefault))
                {
                    result = new GenericErrorContainer(type);
                }
                return true;
            }
            return false;
        }

        public GenericErrorContainer(INamedTypeSymbol type) : base(type)
        {
        }

        protected override string? CustomReplacer(ISymbol? type, string fullname, string? result)
        {
            return applyReplacer(ProjectManager.Config.replacer.genericError, fullname, result);
        }

        protected override void DefineFullname(List<string> result)
        {
            if (IsConvertible)
            {
                string typeName = "\"" + Tools.GetFullName(type) + ", " + type.ContainingAssembly.Name + "\"";
                Type? realType = Tools.GetCompiledType(type.BaseType);
                AddTxt("/** Fullname of the class */", result);
                if (
                    (realType != null && !realType.IsInterface && !realType.IsAbstract && realType != typeof(object) && extends.Count > 0) ||
                    (realType != null && realType.IsAssignableTo(typeof(GenericError)))
                    )
                {
                    AddTxt("public static override get Fullname(): string { return " + typeName + "; }", result);
                }
                else
                {
                    AddTxt("public static get Fullname(): string { return " + typeName + "; }", result);
                }
            }
        }
    }
}
