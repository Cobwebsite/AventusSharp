using AventusSharp.Tools.Attributes;
using AventusSharp.WebSocket.Event;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpToTypescript.Container
{
    internal class NormalClassContainer : BaseClassContainer
    {
        public static bool Is(INamedTypeSymbol type, string fileName, out BaseContainer? result)
        {
            result = null;
            if (Tools.HasAttribute<Export>(type))
            {
                result = new NormalClassContainer(type);
                return true;
            }
            return false;
        }

        public NormalClassContainer(INamedTypeSymbol type) : base(type)
        {
        }

        protected override string? CustomReplacer(ISymbol? type, string fullname, string? result)
        {
            return applyReplacer(ProjectManager.Config.replacer.normalClass, fullname, result);
        }
    }
}
