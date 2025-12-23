using AventusSharp.Data.Attributes;
using AventusSharp.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AventusSharp.Data.Manager
{
    public class DataMemberInfo
    {
        public static DataMemberInfo? Create(object o)
        {
            if (o is PropertyInfo p)
            {
                return new DataMemberInfo(p);
            }
            if (o is FieldInfo f)
            {
                return new DataMemberInfo(f);
            }
            return null;
        }
        public readonly MemberInfo MemberInfo;
        public DataMemberInfo(FieldInfo fieldInfo)
        {
            MemberInfo = fieldInfo;
        }
        public DataMemberInfo(PropertyInfo propertyInfo)
        {
            MemberInfo = propertyInfo;
        }
        public DataMemberInfo(MemberInfo memberInfo)
        {
            this.MemberInfo = memberInfo;
        }

        #region merge info
        /// <summary>
        /// Interface for Type
        /// </summary>
        public Type? Type
        {
            get
            {
                if (MemberInfo is FieldInfo fieldInfo)
                {
                    return fieldInfo.FieldType;
                }
                else if (MemberInfo is PropertyInfo propertyInfo)
                {
                    return propertyInfo.PropertyType;
                }
                return null;
            }
        }
        /// <summary>
        /// Interface for Name
        /// </summary>
        public string Name
        {
            get
            {
                if (MemberInfo != null)
                {
                    return MemberInfo.Name;
                }
                return "";
            }
        }
        /// <summary>
        /// Interface for IsNullable
        /// </summary>
        public bool IsNullable
        {
            get
            {
                if (MemberInfo is PropertyInfo propertyInfo)
                {
                    NullabilityInfo info = new NullabilityInfoContext().Create(propertyInfo);
                    if (info.WriteState == NullabilityState.Nullable || info.ReadState == NullabilityState.Nullable)
                    {
                        return true;
                    }
                }
                else if (MemberInfo is FieldInfo fieldInfo)
                {
                    NullabilityInfo info = new NullabilityInfoContext().Create(fieldInfo);
                    if (info.WriteState == NullabilityState.Nullable || info.ReadState == NullabilityState.Nullable)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        /// <summary>
        /// Interface for GetCustomAttribute
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T? GetCustomAttribute<T>() where T : Attribute
        {
            return MemberInfo.GetCustomAttribute<T>();
        }
        /// <summary>
        /// Interface for GetCustomAttributes
        /// </summary>
        /// <param name="inherit"></param>
        /// <returns></returns>
        public List<object> GetCustomAttributes(bool inherit)
        {
            try
            {
                if (MemberInfo is FieldInfo fieldInfo)
                {
                    return fieldInfo.GetCustomAttributes(inherit).ToList();
                }
                else if (MemberInfo is PropertyInfo propertyInfo)
                {
                    return propertyInfo.GetCustomAttributes(inherit).ToList();

                }
            }
            catch (Exception e)
            {
                new DataError(DataErrorCode.UnknowError, e).Print();
            }
            return new List<object>();

        }

        public T? GetAttribute<T>(bool inherit) where T : class
        {
            List<object> attrs = GetCustomAttributes(inherit);
            foreach (object attr in attrs)
            {
                if (attr is T casted)
                {
                    return casted;
                }
            }
            return null;
        }

        /// <summary>
        /// Interface for GetValue
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public object? GetValue(object? obj)
        {
            if (MemberInfo is FieldInfo fieldInfo)
            {
                return fieldInfo.GetValue(obj);
            }
            else if (MemberInfo is PropertyInfo propertyInfo)
            {
                return propertyInfo.GetValue(obj);

            }
            return null;

        }
        /// <summary>
        /// Interface for set value
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="value"></param>
        public void SetValue(object obj, object? value)
        {
            if (MemberInfo is FieldInfo fieldInfo)
            {
                fieldInfo.SetValue(obj, value);
            }
            else if (MemberInfo is PropertyInfo propertyInfo)
            {
                propertyInfo.SetValue(obj, value);
            }
        }
        /// <summary>
        /// Interface for ReflectedType
        /// </summary>
        public Type? ReflectedType
        {
            get
            {
                if (MemberInfo is FieldInfo fieldInfo)
                {
                    return fieldInfo.ReflectedType;
                }
                else if (MemberInfo is PropertyInfo propertyInfo)
                {
                    return propertyInfo.ReflectedType;
                }
                return null;

            }
        }
        #endregion

        public override string ToString()
        {
            string attrs = "";
            List<object> attributes = GetCustomAttributes(true);
            if (attributes.Count > 0)
            {
                attrs = "- " + string.Join(", ", attributes.Select(a => "[" + a.GetType().Name + "]"));
            }
            Type? type = Type;
            if (type != null)
            {
                string typeTxt = type.Name;
                if (!TypeTools.IsPrimitiveType(type))
                {
                    typeTxt += " - " + type.Assembly.GetName().Name;
                }
                return Name + " (" + type + ") " + attrs;
            }
            return Name + " (NULL)";
        }
    }
}
