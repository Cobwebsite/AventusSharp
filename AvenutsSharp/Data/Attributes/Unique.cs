using AventusSharp.Data.Manager;
using AventusSharp.Tools;
using System;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Type = System.Type;

namespace AventusSharp.Data.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class Unique : ValidationAttribute
    {
        protected Func<object, int, IResultWithError>? prepare;
        protected object? query;
        protected string message;

        public Unique()
        {
            message = "The field must be unique";
        }
        public Unique(string message)
        {
            this.message = message;
        }

        public override ValidationResult IsValid(object? value, ValidationContext context)
        {
            if (value == null) return ValidationResult.Success;

            if (query == null && context.TableInfo.DM != null && context.ReflectedType != null)
            {
                MethodInfo? m = GetType().GetMethod("LoadQuery", BindingFlags.NonPublic | BindingFlags.Instance);
                if (m == null)
                {
                    throw new Exception("Impossible");
                }
                m = m.MakeGenericMethod(context.FieldType);
                m.Invoke(this, new object?[] { context.TableInfo.DM, context.ReflectedType, context, value });
            }

            if (prepare != null && query != null && context.Item != null)
            {
                IResultWithError? resultWithError = prepare(value, context.Item.Id);
                if (resultWithError != null)
                {
                    if (resultWithError.Errors.Count > 0)
                    {
                        ValidationResult validationResult = new ValidationResult();
                        validationResult.Errors.AddRange(resultWithError.Errors);
                        return validationResult;
                    }
                    if (resultWithError.Result is IList list)
                    {
                        if (list.Count > 0)
                        {
                            return new ValidationResult(message, context.FieldName);
                        }
                    }
                }
            }

            return ValidationResult.Success;
        }


        private void LoadQuery<T>(IGenericDM dm, Type type, ValidationContext context, T? value)
        {
            query = dm.GetType().GetMethod("CreateQuery")?.MakeGenericMethod(type).Invoke(dm, null);
            if (query == null)
            {
                DataError error = new DataError(DataErrorCode.ErrorCreatingReverseQuery, "Can't create the query");
                throw error.GetException();
            }
            
            MethodInfo? whereWithParam = query.GetType().GetMethod("WhereWithParameters");
            if (whereWithParam == null)
            {
                DataError error = new DataError(DataErrorCode.ErrorCreatingReverseQuery, "Can't get the function whereWithParam");
                throw error.GetException();
            }

            // t
            ParameterExpression argParam = Expression.Parameter(context.TableInfo.Type, "t");

            // t.$FieldName
            Expression nameProperty = Expression.PropertyOrField(argParam, context.FieldName);

            // value
            Expression<Func<T?>> propLambda = () => value;

            Type? typeIfNullable = System.Nullable.GetUnderlyingType(nameProperty.Type);
            if (typeIfNullable != null)
            {
                nameProperty = Expression.Call(nameProperty, "GetValueOrDefault", Type.EmptyTypes);
            }
            // t.$FieldName == value
            Expression e1 = Expression.Equal(nameProperty, propLambda.Body);

            // t.Id
            Expression idProperty = Expression.PropertyOrField(argParam, Storable.Id);
            int id = 0;
            Expression<Func<int>> idLambda = () => id;
            // t.Id != id
            Expression e2 = Expression.NotEqual(idProperty, idLambda.Body);

            // t.$FieldName == value && t.Id != id
            Expression e3 = Expression.AndAlso(e1, e2);

            // t => t.$FieldName == value && t.Id != id
            LambdaExpression lambda = Expression.Lambda(e3, argParam);
            var prepared = whereWithParam.Invoke(query, new object[] { lambda });
            if (prepared is QueryBuilderPrepared<T> preparedQuery)
            {
                prepare = (object value, int id) =>
                {
                    return preparedQuery.New().SetVariables((define) =>
                    {
                        define("value", value);
                        define("id", id);
                    }).RunWithError();
                };

            }
        }
    }
}
