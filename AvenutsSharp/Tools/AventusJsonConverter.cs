using AventusSharp.Data.CustomTableMembers;
using AventusSharp.Tools.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AventusSharp.Tools
{
    /// <summary>
    /// Custom converter to add type when we need it (avoid dico and list bc crash in js)
    /// </summary>
    public class AventusJsonConverter : JsonConverter
    {
        private readonly List<string> propToRemove = new() { };

        /// <summary>
        /// always true because we can always convert until object
        /// </summary>
        /// <param name="objectType"></param>
        /// <returns></returns>
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        /// <param name="reader"></param>
        /// <param name="objectType"></param>
        /// <param name="existingValue"></param>
        /// <param name="serializer"></param>
        /// <returns></returns>
        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            return CloneNoConverter(serializer).Deserialize(reader, objectType);
        }

        protected string GetTypeName(Type type)
        {
            if (type == typeof(GenericError))
            {
                return "Aventus.GenericError";
            }
            return type.FullName?.Split('`')[0] + ", " + type.Assembly.GetName().Name;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="serializer"></param>
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                return;
            }
            lock (value)
            {
                Type type = value.GetType();
                if (type.IsPrimitive || TypeTools.IsPrimitiveType(type))
                {
                    JToken t = JToken.FromObject(value);
                    t.WriteTo(writer);
                }
                else if (type.BaseType != null && type.BaseType == typeof(Enum))
                {
                    JToken t = JToken.FromObject(value);
                    t.WriteTo(writer);
                }
                else if (type.IsGenericType && type.GetInterfaces().Contains(typeof(IDictionary)))
                {
                    IEnumerable? keys = (IEnumerable?)type.GetProperty("Keys")?.GetValue(value, null);
                    IEnumerable? values = (IEnumerable?)type.GetProperty("Values")?.GetValue(value, null);
                    if (keys == null || values == null)
                    {
                        return;
                    }
                    JObject jo = new()
                    {
                        { "$type", "Aventus.Map" }
                    };
                    IEnumerator valueEnumerator = values.GetEnumerator();
                    JArray joArray = new();
                    foreach (object key in keys)
                    {
                        valueEnumerator.MoveNext();
                        if (valueEnumerator.Current != null)
                        {
                            JArray keyValue = new JArray
                            {
                                JToken.FromObject(key, serializer),
                                JToken.FromObject(valueEnumerator.Current, serializer)
                            };
                            joArray.Add(keyValue);

                        }
                    }
                    jo.Add("values", joArray);
                    jo.WriteTo(writer);
                }
                else if (type.IsGenericType && type.GetInterfaces().Contains(typeof(IList)))
                {
                    IEnumerable values = (IEnumerable)value;
                    IEnumerator valueEnumerator = values.GetEnumerator();
                    JArray jo = new();
                    while (valueEnumerator.MoveNext())
                    {
                        if (valueEnumerator.Current != null)
                        {
                            jo.Add(JToken.FromObject(valueEnumerator.Current, serializer));
                        }

                    }
                    jo.WriteTo(writer);
                }
                else if (type.GetInterfaces().Contains(typeof(IStorableList)))
                {
                    IEnumerable values = (IEnumerable)value;
                    IEnumerator valueEnumerator = values.GetEnumerator();
                    JArray jo = new();
                    while (valueEnumerator.MoveNext())
                    {
                        if (valueEnumerator.Current != null)
                        {
                            jo.Add(JToken.FromObject(valueEnumerator.Current, serializer));
                        }

                    }
                    jo.WriteTo(writer);
                }
                else
                {
                    JObject jo = new()
                    {
                        { "$type", GetTypeName(type) }
                    };

                    foreach (PropertyInfo prop in type.GetProperties())
                    {
                        if (prop.GetCustomAttribute<NoExport>() != null) continue;
                        if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                        {
                            try
                            {
                                object? propVal = prop.GetValue(value, null);
                                if (propVal != null)
                                {
                                    if (!propToRemove.Contains(prop.Name) && !jo.ContainsKey(prop.Name))
                                    {
                                        jo.Add(prop.Name, JToken.FromObject(propVal, serializer));
                                    }
                                }
                            }
                            catch
                            {
                                Console.WriteLine(type.Name);
                                Console.WriteLine(prop.Name);
                                Console.WriteLine(value);
                                throw;
                            }
                        }
                    }

                    foreach (FieldInfo prop in type.GetFields())
                    {
                        if (prop.GetCustomAttribute<NoExport>() != null) continue;
                        object? propVal = prop.GetValue(value);
                        if (propVal != null)
                        {
                            if (!propToRemove.Contains(prop.Name) && !jo.ContainsKey(prop.Name))
                            {
                                jo.Add(prop.Name, JToken.FromObject(propVal, serializer));
                            }
                        }

                    }
                    jo.WriteTo(writer);
                }
            }
        }


        private static JsonSerializer? _cloneConverter;
        private static JsonSerializer CloneNoConverter(JsonSerializer settings)
        {
            if (_cloneConverter == null)
            {
                JsonSerializer serializer = JsonSerializer.Create();
                // if (!CollectionUtils.IsNullOrEmpty(settings.Converters))
                // {
                //     // insert settings converters at the beginning so they take precedence
                //     // if user wants to remove one of the default converters they will have to do it manually
                //     for (int i = 0; i < settings.Converters.Count; i++)
                //     {
                //         serializer.Converters.Insert(i, settings.Converters[i]);
                //     }
                // }

                // serializer specific
                serializer.TypeNameHandling = settings.TypeNameHandling;
                serializer.MetadataPropertyHandling = settings.MetadataPropertyHandling;
                serializer.TypeNameAssemblyFormatHandling = settings.TypeNameAssemblyFormatHandling;
                serializer.PreserveReferencesHandling = settings.PreserveReferencesHandling;
                serializer.ReferenceLoopHandling = settings.ReferenceLoopHandling;
                serializer.MissingMemberHandling = settings.MissingMemberHandling;
                serializer.ObjectCreationHandling = settings.ObjectCreationHandling;
                serializer.NullValueHandling = settings.NullValueHandling;
                serializer.DefaultValueHandling = settings.DefaultValueHandling;
                serializer.ConstructorHandling = settings.ConstructorHandling;
                serializer.Context = settings.Context;


                if (settings.ContractResolver != null)
                {
                    serializer.ContractResolver = settings.ContractResolver;
                }
                if (settings.TraceWriter != null)
                {
                    serializer.TraceWriter = settings.TraceWriter;
                }
                if (settings.EqualityComparer != null)
                {
                    serializer.EqualityComparer = settings.EqualityComparer;
                }
                if (settings.SerializationBinder != null)
                {
                    serializer.SerializationBinder = settings.SerializationBinder;
                }

                // reader/writer specific
                // unset values won't override reader/writer set values
                serializer.Formatting = settings.Formatting;
                serializer.DateFormatHandling = settings.DateFormatHandling;
                serializer.DateTimeZoneHandling = settings.DateTimeZoneHandling;
                serializer.DateParseHandling = settings.DateParseHandling;
                serializer.DateFormatString = settings.DateFormatString;
                serializer.FloatFormatHandling = settings.FloatFormatHandling;
                serializer.FloatParseHandling = settings.FloatParseHandling;
                serializer.StringEscapeHandling = settings.StringEscapeHandling;
                serializer.Culture = settings.Culture;
                serializer.MaxDepth = settings.MaxDepth;
                _cloneConverter = serializer;
            }
            return _cloneConverter;
        }
    }
}
