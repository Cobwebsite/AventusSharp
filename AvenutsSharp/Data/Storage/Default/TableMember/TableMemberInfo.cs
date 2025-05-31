using AventusSharp.Data.Attributes;
using AventusSharp.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Data;
using AventusSharp.Data.Manager;
using System.ComponentModel.DataAnnotations;
using ValidationAttribute = AventusSharp.Data.Attributes.ValidationAttribute;
using ValidationContext = AventusSharp.Data.Attributes.ValidationContext;
using ValidationResult = AventusSharp.Data.Attributes.ValidationResult;
using Org.BouncyCastle.Asn1.Cms;
using Attribute = System.Attribute;
using System.Data.SqlTypes;

namespace AventusSharp.Data.Storage.Default.TableMember
{
    
    public abstract class TableMemberInfo
    {
        public static TableMemberInfo? Create(MemberInfo fieldInfo, TableInfo tableInfo)
        {
            if (fieldInfo.GetCustomAttribute<ReverseLink>() != null)
            {
                return new TableReverseMemberInfo(fieldInfo, tableInfo);
            }
            if(fieldInfo.GetCustomAttribute<NotInDB> () != null)
            {
                return null;
            }
            return TableMemberInfoSql.CreateSql(fieldInfo, tableInfo);
        }

        public MemberInfo? memberInfo { get; protected set; }
        protected Dictionary<Type, MemberInfo> memberInfoByType = new();
        public TableInfo TableInfo { get; private set; }
        public IGenericDM? DM { get => TableInfo.DM; }

        public TableMemberInfo(TableInfo tableInfo)
        {
            TableInfo = tableInfo;
        }
        public TableMemberInfo(FieldInfo fieldInfo, TableInfo tableInfo) : this(tableInfo)
        {
            memberInfo = fieldInfo;
            ParseAttributes();
        }
        public TableMemberInfo(PropertyInfo propertyInfo, TableInfo tableInfo) : this(tableInfo)
        {
            memberInfo = propertyInfo;
            ParseAttributes();
        }
        public TableMemberInfo(MemberInfo? memberInfo, TableInfo tableInfo) : this(tableInfo)
        {
            this.memberInfo = memberInfo;
            ParseAttributes();
        }

      

        public void ChangeTableInfo(TableInfo tableInfo)
        {
            TableInfo = tableInfo;
        }

       
        public List<GenericError> IsValid(object? o, object? rootValue, StorableAction action)
        {
            List<GenericError> errors = new();
            IStorable? storable = null;
            if(rootValue is IStorable storableTemp) {
                storable = storableTemp;
            }
            ValidationContext context = new(Name, MemberType, ReflectedType, TableInfo, action, storable);
            foreach (var validationAttribute in ValidationAttributes)
            {
                ValidationResult result = validationAttribute.IsValid(o, context);
                errors.AddRange(result.Errors);
            }

            return errors;
        }


        #region attributes 

        public bool IsAutoCreate { get; protected set; } = true;
        public bool IsAutoUpdate { get; protected set; } = true;
        public bool IsAutoDelete { get; protected set; } = true;
        public bool IsAutoRead { get; protected set; } = true;
        public bool NotInDB { get; protected set; } = false;


        protected readonly List<ValidationAttribute> ValidationAttributes = new();

        protected virtual void ParseAttributes()
        {
            List<object> attributes = GetCustomAttributes(false);
            foreach (object attribute in attributes)
            {
                if(attribute is Attribute attr)
                {
                    ParseAttribute(attr);
                }
            }
        }

        /// <summary>
        /// Return true if the loop must be break
        /// </summary>
        /// <param name="attribute"></param>
        /// <returns></returns>
        protected virtual bool ParseAttribute(Attribute attribute)
        {
            if (attribute is NotInDB)
            {
                NotInDB = true;
                return true;
            }
            if (attribute is AutoCreate autoCreate)
            {
                IsAutoCreate = autoCreate.Is;
                return true;
            }
            if (attribute is AutoUpdate autoUpdate)
            {
                IsAutoUpdate = autoUpdate.Is;
                return true;
            }
            if (attribute is AutoDelete autoDelete)
            {
                IsAutoDelete = autoDelete.Is;
                return true;

            }
            if (attribute is AutoRead autoRead)
            {
                IsAutoRead = autoRead.Is;
                return true;

            }
            if (attribute is AutoCUD autoCUD)
            {
                IsAutoCreate = autoCUD.Is;
                IsAutoUpdate = autoCUD.Is;
                IsAutoDelete = autoCUD.Is;
                return true;

            }
            if (attribute is AutoCRUD autoCRUD)
            {
                IsAutoCreate = autoCRUD.Is;
                IsAutoUpdate = autoCRUD.Is;
                IsAutoDelete = autoCRUD.Is;
                IsAutoRead = autoCRUD.Is;
                return true;
            }
            if (attribute is ValidationAttribute validationAttribute)
            {
                ValidationAttributes.Add(validationAttribute);
                return false;
            }
            return false;
        }

        /// <summary>
        /// Return the value for the storage
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public virtual object? GetValueToSave(object? obj)
        {
            if(obj == null) return null;
            return GetValue(obj);
        }
        #endregion

        #region merge info
        /// <summary>
        /// Interface for Type
        /// </summary>
        public virtual Type MemberType
        {
            get
            {
                Type? type = null;
                if (memberInfo is FieldInfo fieldInfo)
                {
                    type = fieldInfo.FieldType;
                }
                else if (memberInfo is PropertyInfo propertyInfo)
                {
                    type =propertyInfo.PropertyType;
                }
                if (type != null)
                {
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        type = type.GetGenericArguments()[0];
                    }
                    return type;
                }
                throw new Exception("No type for field??");
            }
        }

        /// <summary>
        /// Interface for Type
        /// </summary>
        public virtual Type MemberTypeWithNullable
        {
            get
            {
                if (memberInfo is FieldInfo fieldInfo)
                {
                    return fieldInfo.FieldType;
                }
                else if (memberInfo is PropertyInfo propertyInfo)
                {
                    return propertyInfo.PropertyType;
                }
                throw new Exception("No type for field??");
            }
        }
        /// <summary>
        /// Interface for Name
        /// </summary>
        public virtual string Name
        {
            get
            {
                if (memberInfo != null)
                {
                    return memberInfo.Name;
                }
                return "";
            }
        }
        /// <summary>
        /// Interface for GetCustomAttribute
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public virtual T? GetCustomAttribute<T>() where T : Attribute
        {
            return memberInfo?.GetCustomAttribute<T>();
        }
        /// <summary>
        /// Interface for GetCustomAttributes
        /// </summary>
        /// <param name="inherit"></param>
        /// <returns></returns>
        public virtual List<object> GetCustomAttributes(bool inherit)
        {
            try
            {
                if (memberInfo is FieldInfo fieldInfo)
                {
                    return fieldInfo.GetCustomAttributes(inherit).ToList();
                }
                else if (memberInfo is PropertyInfo propertyInfo)
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

        /// <summary>
        /// Interface for GetValue
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public virtual object? GetValue(object obj)
        {
            try
            {
                MemberInfo? member = GetRealMember(obj);
                if (member is FieldInfo fieldInfo)
                {
                    return fieldInfo.GetValue(obj);
                }
                else if (member is PropertyInfo propertyInfo)
                {
                    return propertyInfo.GetValue(obj);

                }
            }
            catch (Exception e)
            {
                new DataError(DataErrorCode.UnknowError, e).Print();
            }
            return null;

        }
        /// <summary>
        /// Interface for set value
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="value"></param>
        public virtual void SetValue(object obj, object? value)
        {
            try
            {
                MemberInfo? member = GetRealMember(obj);
                if (member is FieldInfo fieldInfo)
                {
                    fieldInfo.SetValue(obj, value);
                }
                else if (member is PropertyInfo propertyInfo)
                {
                    propertyInfo.SetValue(obj, value);

                }
            }
            catch (Exception e)
            {
                new DataError(DataErrorCode.UnknowError, e).Print();
            }
        }

        /// <summary>
        /// Interface for ReflectedType
        /// </summary>
        public virtual Type? ReflectedType
        {
            get
            {
                if (memberInfo is FieldInfo fieldInfo)
                {
                    return fieldInfo.ReflectedType;
                }
                else if (memberInfo is PropertyInfo propertyInfo)
                {

                    return propertyInfo.ReflectedType;
                }
                return null;

            }
        }

        /// <summary>
        /// Avoid Getting a member with generic param
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        protected MemberInfo? GetRealMember(object obj)
        {
            try
            {
                MemberInfo? member = memberInfo;
                if (member?.ReflectedType?.IsGenericType == true)
                {
                    Type typeToUse = obj.GetType();
                    if (!memberInfoByType.ContainsKey(typeToUse))
                    {
                        MemberInfo[] members = typeToUse.GetMember(member.Name);
                        if (members.Length == 0)
                        {
                            return null;
                        }
                        memberInfoByType.Add(typeToUse, members[0]);
                    }
                    member = memberInfoByType[typeToUse];
                }

                if (member == null || member.ReflectedType == null || !member.ReflectedType.IsInstanceOfType(obj))
                {
                    return null;
                }

                return member;
            }
            catch (Exception e)
            {
                new DataError(DataErrorCode.UnknowError, e).Print();
            }
            return null;
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
            Type? type = MemberType;
            if (type != null)
            {
                string typeTxt = type.Name;
                if (!TypeTools.IsPrimitiveType(MemberType))
                {
                    typeTxt += " - " + type.Assembly.GetName().Name;
                }
                return Name + " (" + typeTxt + ") " + attrs;
            }
            return Name + "(NULL)";
        }
    }
}
