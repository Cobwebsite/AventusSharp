using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AventusSharp.Tools
{
    public static class InterfaceDumperExtension
    {
        /// <summary>
        /// List interfaces of an interface
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Type[] GetCurrentInterfaces(this Type @type)
        {
            //All of the interfaces implemented by the class
            HashSet<Type> allInterfaces = new(@type.GetInterfaces());

            //Type one step down the hierarchy
            Type? baseType = @type.BaseType;

            //If it is not null, it might implement some other interfaces
            if (baseType != null)
            {
                //So let us remove all the interfaces implemented by the base class
                allInterfaces.ExceptWith(baseType.GetInterfaces());
            }


            HashSet<Type> toRemove = new();
            //Considering class A given above allInterfaces contain A and B now
            foreach (Type implementedByMostDerivedClass in allInterfaces)
            {
                //For interface A this will only contain single element, namely B
                //For interface B this will an empty array
                foreach (Type implementedByOtherInterfaces in implementedByMostDerivedClass.GetInterfaces())
                {
                    toRemove.Add(implementedByOtherInterfaces);
                }
            }

            //Finally remove the interfaces that do not belong to the most derived class.
            allInterfaces.ExceptWith(toRemove);

            //Result
            return allInterfaces.ToArray();
        }
    }
}
