using AventusSharp.Localization;
using AventusSharp.Tools;
using HttpMultipartParser;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AventusSharp.Hosting;

namespace AventusSharp.Routes.Request
{
    public class RouterBody
    {
        private Dictionary<string, HttpFile> files = new Dictionary<string, HttpFile>();
        private IAventusContext context;
        private JToken data = new JObject();
        public RouterBody(IAventusContext context)
        {
            this.context = context;
        }


        internal async Task<VoidWithRouteError> Parse()
        {
            VoidWithRouteError result = new();
            string? contentType = context.Request.ContentType?.ToLower();
            if (!string.IsNullOrEmpty(contentType))
            {
                if (contentType.StartsWith("multipart/form-data"))
                {
                    return await ParseMultiPartForm();
                }
                if (contentType.StartsWith("application/json"))
                {
                    return await ParseJson();
                }
                if (contentType.Split(';')[0].Trim() == "application/x-www-form-urlencoded")
                {
                    return await ParseUrlEncodedForm();
                }
                result.Errors.Add(new RouteError(RouteErrorCode.FormContentTypeUnknown, AventusTranslations.Get(AventusMessageKeys.Routes.UnsupportedContentType, contentType)));
            }
            else
            {
                result.Errors.Add(new RouteError(RouteErrorCode.FormContentTypeUnknown, AventusTranslations.Get(AventusMessageKeys.Routes.UnsupportedContentType, contentType)));
            }
            return result;
        }

        private async Task<VoidWithRouteError> ParseUrlEncodedForm()
        {
            VoidWithRouteError result = new();
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                string body = await reader.ReadToEndAsync();
                foreach (string pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    int separator = pair.IndexOf('=');
                    string name = pair;
                    string value = "";
                    if (separator >= 0)
                    {
                        name = pair.Substring(0, separator);
                        value = pair.Substring(separator + 1);
                    }
                    result.Run(() =>
                        AddFormValue(WebUtility.UrlDecode(name), WebUtility.UrlDecode(value))
                    );
                }
            }
            catch (FormatException e)
            {
                result.Errors.Add(new(RouteErrorCode.InvalidFormData, e));
            }
            catch (Exception e)
            {
                result.Errors.Add(new(RouteErrorCode.UnknownError, e));
            }
            return result;
        }

        private VoidWithRouteError AddFormValue(string name, string value)
        {
            VoidWithRouteError result = new();
            bool append = name.EndsWith("[]", StringComparison.Ordinal);
            if (append)
            {
                name = name.Substring(0, name.Length - 2);
            }
            string[] parts = Regex.Replace(name, @"\[(.*?)\]", ".$1").Split('.');
            JToken container = data;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length == 0)
                {
                    result.Errors.Add(new(RouteErrorCode.InvalidFormData, AventusTranslations.Get(AventusMessageKeys.Routes.EmptyFormPathSegment)));
                    return result;
                }

                int index = -1;
                if (container is JArray array)
                {
                    if (!int.TryParse(part, out index) || index < 0 || index > array.Count)
                    {
                        result.Errors.Add(new(RouteErrorCode.InvalidFormData, AventusTranslations.Get(AventusMessageKeys.Routes.InvalidFormArrayIndices)));
                        return result;
                    }
                    if (index == array.Count)
                    {
                        array.Add(JValue.CreateNull());
                    }
                }
                else if (container is not JObject)
                {
                    result.Errors.Add(new(RouteErrorCode.InvalidFormData, AventusTranslations.Get(AventusMessageKeys.Routes.ConflictingFormPaths)));
                    return result;
                }

                JToken? current = container is JArray ? container[index] : container[part];
                JToken next;
                if (i == parts.Length - 1)
                {
                    if (current == null || current.Type == JTokenType.Null)
                    {
                        next = new JValue(value);
                        if (append)
                        {
                            next = new JArray(next);
                        }
                    }
                    else if (current is JArray values)
                    {
                        values.Add(value);
                        return result;
                    }
                    else if (current is JValue)
                    {
                        next = new JArray(current.DeepClone(), new JValue(value));
                    }
                    else
                    {
                        result.Errors.Add(new(RouteErrorCode.InvalidFormData, AventusTranslations.Get(AventusMessageKeys.Routes.ConflictingFormPaths)));
                        return result;
                    }
                }
                else
                {
                    next = current!;
                    if (current == null || current.Type == JTokenType.Null)
                    {
                        next = int.TryParse(parts[i + 1], out _) ? new JArray() : new JObject();
                    }
                }
                if (!ReferenceEquals(current, next))
                {
                    if (container is JArray)
                    {
                        container[index] = next;
                    }
                    else
                    {
                        container[part] = next;
                    }
                }
                container = next;
            }
            return result;
        }

        private async Task<VoidWithRouteError> ParseMultiPartForm()
        {
            VoidWithRouteError result = new();
            try
            {
                Dictionary<string, string> bodyJSON = new Dictionary<string, string>();
                StreamingMultipartFormDataParser parser = new StreamingMultipartFormDataParser(context.Request.Body);
                parser.ParameterHandler += parameter =>
                {
                    string name = Regex.Replace(parameter.Name, @"\[(.*?)\]", ".$1");
                    string[] splitted = name.Split(".");
                    JToken? container = data;
                    for (int i = 0; i < splitted.Length; i++)
                    {
                        if (container == null) return;
                        Action<JToken> set = (obj) => container[splitted[i]] = obj;
                        Func<JToken?> get = () => { return container[splitted[i]]; };

                        if (container is JArray array)
                        {
                            int key = int.Parse(splitted[i]);
                            set = (obj) => container[key] = obj;
                            get = () => { return container[key]; };
                        }
                        if (i + 1 < splitted.Length)
                        {
                            int nb;
                            if (int.TryParse(splitted[i + 1], out nb))
                            {
                                if (get() == null)
                                {
                                    set(new JArray());
                                }
                                container = get();
                            }
                            else
                            {
                                if (get() == null)
                                {
                                    set(new JObject());
                                }
                                container = get();
                            }
                        }
                        else
                        {
                            set(parameter.Data);
                        }
                    }
                };
                parser.FileHandler += (name, fileName, type, disposition, buffer, bytes, partNumber, additionalProperties) =>
                {
                    string realName = Regex.Replace(name, @"\[(.*?)\]", ".$1");
                    if (partNumber == 0)
                    {
                        if (!files.ContainsKey(realName))
                        {
                            string tempFolder = RouterMiddleware.config.FileUploadTempDir;
                            if (!Directory.Exists(tempFolder))
                            {
                                Directory.CreateDirectory(tempFolder);
                            }
                            string filePath = Path.Combine(tempFolder, fileName);
                            if (File.Exists(filePath))
                            {
                                File.Delete(filePath);
                            }
                            HttpFile file = new HttpFile(
                                name,
                                fileName,
                                filePath,
                                type,
                                new FileStream(filePath, FileMode.Create),
                                RouterMiddleware.config.FileUploadTempDir);
                            files.Add(realName, file);
                        }
                    }
                    files[realName].stream?.Write(buffer, 0, bytes);

                };

                // You can parse synchronously:
                await parser.RunAsync();
                foreach (HttpFile file in files.Values)
                {
                    file.stream?.Close();
                    file.stream?.Dispose();
                }
            }
            catch (Exception e)
            {
                result.Errors.Add(new(RouteErrorCode.UnknownError, e));
            }
            return result;
        }

        private async Task<VoidWithRouteError> ParseJson()
        {
            VoidWithRouteError result = new();
            try
            {
                using (var reader = new StreamReader(context.Request.Body))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    data = await JToken.LoadAsync(jsonReader);
                    if (data is not JObject && data is not JArray)
                        throw new JsonReaderException("The JSON request body must be an object or an array.");
                }
            }
            catch (Exception e)
            {
                result.Errors.Add(new RouteError(RouteErrorCode.UnknownError, e));
            }
            return result;
        }

        public HttpFile? GetFile(string propPath)
        {
            if (files.Count > 0)
            {
                return files.Values.FirstOrDefault(f => f.FormName == propPath);
            }
            return null;
        }
        public List<HttpFile> GetFiles(string propPath)
        {
            Regex regex = new Regex(propPath + "\\[[0-9]+\\]");
            return files.Values.Where(f => regex.IsMatch(f.FormName)).ToList();
        }

        /// <summary>
        /// Find reference inside object to add File
        /// </summary>
        /// <param name="propPath"></param>
        /// <param name="result"></param>
        protected void AddFileToResult(string propPath, object result)
        {
            foreach (KeyValuePair<string, HttpFile> fileStored in files)
            {
                if (fileStored.Key.StartsWith(propPath))
                {
                    string missingPath = fileStored.Key.Replace(propPath + ".", "");
                    string[] splitted = missingPath.Split(".");
                    object? current = result;
                    for (int i = 0; i < splitted.Length - 1; i++)
                    {
                        string s = splitted[i];
                        Func<object?, object?>? fct = null;
                        Action<object?, object?>? setTemp = null;
                        Type? typeFieldGet = null;

                        PropertyInfo? propertyInfoGet = current?.GetType().GetProperty(s, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        if (propertyInfoGet != null)
                        {
                            fct = propertyInfoGet.GetValue;
                            typeFieldGet = propertyInfoGet.PropertyType;
                            setTemp = propertyInfoGet.SetValue;
                        }
                        else
                        {
                            FieldInfo? fieldInfoGet = current?.GetType().GetField(s, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                            if (fieldInfoGet != null)
                            {
                                fct = fieldInfoGet.GetValue;
                                typeFieldGet = fieldInfoGet.FieldType;
                                setTemp = fieldInfoGet.SetValue;
                            }
                        }

                        if (fct == null)
                        {
                            break;
                        }

                        object? temp = fct(current);
                        if (temp == null && setTemp != null && typeFieldGet != null && typeFieldGet.GetInterfaces().Contains(typeof(IList)))
                        {
                            setTemp(current, Activator.CreateInstance(typeFieldGet));
                            temp = fct(current);
                        }
                        current = temp;
                    }

                    if (current == null)
                    {
                        continue;
                    }

                    Action<object?, object?>? set = null;
                    string last = splitted[splitted.Length - 1];
                    PropertyInfo? propertyInfoSet = current?.GetType().GetProperty(last, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (propertyInfoSet != null)
                    {
                        if (propertyInfoSet.PropertyType == typeof(HttpFile))
                        {
                            set = propertyInfoSet.SetValue;
                        }
                        else if (propertyInfoSet.PropertyType == typeof(List<HttpFile>))
                        {
                            set = propertyInfoSet.SetValue;
                            set = (target, data) =>
                            {
                                object? o = propertyInfoSet.GetValue(target);
                                if (o is IList list)
                                {
                                    list.Add(data);
                                }
                            };
                        }
                    }
                    else
                    {
                        FieldInfo? fieldInfoSet = current?.GetType().GetField(last, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                        if (fieldInfoSet != null)
                        {
                            if (fieldInfoSet.FieldType.GetInterfaces().Contains(typeof(HttpFile)))
                            {
                                set = fieldInfoSet.SetValue;
                            }
                        }
                    }

                    if (set == null)
                    {
                        break;
                    }

                    set(current, fileStored.Value);
                }
            }
        }

        /// <summary>
        /// Transform data into object T. Add path to tell where to find data to cast
        /// </summary>
        /// <param name="type">Type needed</param>
        /// <param name="propPath">Path where to find data</param>
        /// <param name="isOptional">Determine if data is required</param>
        /// <returns></returns>
        public ResultWithRouteError<object> GetData(Type type, string propPath, bool isOptional)
        {
            ResultWithRouteError<object> result = new();

            try
            {
                JToken? dataToUse = data;
                string[] props = data is JArray ? Array.Empty<string>() : propPath.Split(".");
                foreach (string prop in props)
                {
                    if (dataToUse == null)
                    {
                        if (!isOptional)
                        {
                            result.Errors.Add(new RouteError(RouteErrorCode.CantGetValueFromBody, AventusTranslations.Get(AventusMessageKeys.Routes.BodyPathNotFound, propPath)));
                            return result;
                        }
                        break;
                    }
                    if (!string.IsNullOrEmpty(prop))
                    {
                        dataToUse = dataToUse[prop];
                        if (dataToUse == null && !isOptional)
                        {
                            result.Errors.Add(new RouteError(RouteErrorCode.CantGetValueFromBody, AventusTranslations.Get(AventusMessageKeys.Routes.BodyPathNotFound, propPath)));
                            return result;
                        }
                    }
                    else
                    {
                        dataToUse = null;
                    }
                }


                object? temp = JsonConvert.DeserializeObject(
                    JsonConvert.SerializeObject(dataToUse),
                    type,
                    RouterMiddleware.config.JSONSettings
                );
                if (temp != null)
                {
                    AddFileToResult(propPath, temp);
                    result.Result = temp;
                }
            }
            catch (Exception e)
            {
                result.Errors.Add(new RouteError(RouteErrorCode.UnknownError, e));
            }
            return result;
        }

    }

}
