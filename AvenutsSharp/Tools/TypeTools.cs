using AventusSharp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace AventusSharp.Tools
{
    public static class TypeTools
    {
        private static Type[] PrimitiveType
        {
            get => new Type[] {
                typeof(Enum),
                typeof(Decimal),
                typeof(String),
                typeof(Boolean),
                typeof(Byte),
                typeof(SByte),
                typeof(Int16),
                typeof(UInt16),
                typeof(Int32),
                typeof(UInt32),
                typeof(Int64),
                typeof(UInt64),
                typeof(IntPtr),
                typeof(UIntPtr),
                typeof(Char),
                typeof(Double),
                typeof(Single),
                typeof(DateTime),
                typeof(TimeSpan),
            };
        }

        public static bool IsPrimitiveType(Type? type)
        {
            if(type == null)
            {
                return false;
            }
            if (PrimitiveType.Contains(type))
            {
                return true;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return PrimitiveType.Contains(type.GetGenericArguments()[0]);
            }
            return false;
        }

        public static string GetReadableName(Type type)
        {
            string generic = "";
            if (type.IsGenericType)
            {
                generic = "<";
                Type[] arguments = type.GetGenericArguments();
                foreach (Type argument in arguments)
                {
                    generic += GetReadableName(argument);
                }
                generic += ">";
            }
            return type.Name.Split('`')[0] + generic;
        }

        public static object CreateNewObj(Type type)
        {
            NewExpression constructorExpression = Expression.New(type);
            Expression<Func<object>> lambdaExpression = Expression.Lambda<Func<object>>(constructorExpression);
            Func<object> createFunc = lambdaExpression.Compile();
            object newObj = createFunc();
            return newObj;
        }
        public static T CreateNewObj<T>(Type type)
        {
            return (T)CreateNewObj(type);
        }
        public static T CreateNewObj<T>() where T : new()
        {
            return (T)CreateNewObj(typeof(T));
        }

        public static ResultWithDataError<Type> GetTypeDataObject(string fullname)
        {
            ResultWithDataError<Type> result = new ResultWithDataError<Type>();
            Func<AssemblyName, Assembly?> func = (assemblieName) =>
            {
                Assembly? a = DataMainManager.searchingAssemblies.Find(a => a.FullName == assemblieName.FullName);
                return a;
            };
            Type? typeToCreate = Type.GetType(fullname, func, null);
            if (typeToCreate == null)
            {
                result.Errors.Add(new DataError(DataErrorCode.WrongType, "Can't find the type " + fullname));
            }
            else
            {
                result.Result = typeToCreate;
            }
            return result;
        }



        internal static string getMemberNameForType(Type type, Expression? exp)
        {
            if (exp is MemberExpression memberExpression)
            {
                if (type.IsInterface && memberExpression.Member.DeclaringType != null && memberExpression.Member.DeclaringType.GetInterfaces().Contains(type))
                {
                    return memberExpression.Member.Name;
                }

                if (memberExpression.Member.ReflectedType == type || memberExpression.Member.ReflectedType == type.BaseType)
                {
                    return memberExpression.Member.Name;
                }

                string memName = getMemberNameForType(type, memberExpression.Expression);
                if (!string.IsNullOrEmpty(memName))
                {
                    return memName + "." + memberExpression.Member.Name;
                }
            }
            return "";
        }
        /// <summary>
        /// Get the string that corresponds to the member passes in <paramref name="memberAccess"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="memberAccess"></param>
        /// <returns></returns>
        public static string GetMemberName<T, TValue>(Expression<Func<T, TValue>> memberAccess)
        {
            return getMemberNameForType(typeof(T), memberAccess.Body);
            //return ((MemberExpression)memberAccess.Body).Member.Name;
        }
    }
}
