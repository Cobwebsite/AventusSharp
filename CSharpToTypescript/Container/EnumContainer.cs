using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpToTypescript.Container
{
    internal class EnumContainer : BaseContainer
    {
        public static bool Is(INamedTypeSymbol type, string fileName, out BaseContainer? result)
        {
            result = null;
            if (type.BaseType != null && type.BaseType.Name == typeof(Enum).Name)
            {
                if (Tools.ExportToTypesript(type, ProjectManager.Config.exportEnumByDefault))
                {
                    result = new EnumContainer(type);
                }
                return true;
            }
            return false;
        }
        public EnumContainer(INamedTypeSymbol type) : base(type)
        {
        }

        protected override string WriteAction()
        {
            List<string> result = new List<string>();
            if (ProjectManager.Config.useNamespace)
            {
                AddIndent();
            }
            AddTxtOpen("export enum " + type.Name + " {", result);
            List<string> fields = new List<string>();
            int shouldBe = 0;
            foreach (var item in type.GetMembers())
            {
                if (item is IFieldSymbol field)
                {
                    if (field.ConstantValue is int cst && cst != shouldBe)
                    {
                        shouldBe = cst;
                        AddTxt(item.Name + " = " + cst, fields);
                    }
                    else
                    {
                        AddTxt(item.Name, fields);
                    }
                }
                shouldBe++;
            }
            result.Add(string.Join(",\r\n", fields));
            AddTxtClose("}", result);
            if (ProjectManager.Config.useNamespace)
            {
                RemoveIndent();
            }

            return string.Join("\r\n", result);
        }
    }
}
