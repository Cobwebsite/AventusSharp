using HttpMultipartParser;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AventusSharp.Routes.Request
{
    public class RouterBody
    {
        private Dictionary<string, HttpFile> files = new Dictionary<string, HttpFile>();
        private HttpContext context;
        private JsonObject data = new JsonObject();
        public RouterBody(HttpContext context)
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
                result.Errors.Add(new RouteError(RouteErrorCode.FormContentTypeUnknown, "The content type " + contentType + " can't be parsed"));
            }
            else
            {
                result.Errors.Add(new RouteError(RouteErrorCode.FormContentTypeUnknown, "The content type " + contentType + " can't be parsed"));
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
                    JsonNode? container = data;
                    for (int i = 0; i < splitted.Length; i++)
                    {
                        if (container == null) return;
                        string key = splitted[i];
                        Action<JsonNode> set = (obj) =>
                        {
                            if (container is JsonObject objContainer)
                                objContainer[key] = obj;
                            else if (container is JsonArray arrContainer)
                                arrContainer[int.Parse(key)] = obj;
                        };
                        Func<JsonNode?> get = () =>
                        {
                            if (container is JsonObject objContainer)
                                return objContainer.ContainsKey(key) ? objContainer[key] : null;
                            else if (container is JsonArray arrContainer)
                                return arrContainer[int.Parse(key)];
                            return null;
                        };


                        if (i + 1 < splitted.Length)
                        {
                            bool isArrayIndex = int.TryParse(splitted[i + 1], out _);

                            if (get() == null)
                            {
                                set(isArrayIndex ? new JsonArray() : new JsonObject());
                            }
                            container = get();

                        }
                        else
                        {
                            set(JsonValue.Create(parameter.Data));
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
                            HttpFile file = new HttpFile(name, fileName, filePath, type, new FileStream(filePath, FileMode.Create));
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
                result.Errors.Add(new(RouteErrorCode.UnknowError, e));
            }
            return result;
        }

        private async Task<VoidWithRouteError> ParseJson()
        {
            VoidWithRouteError result = new();
            try
            {
                context.Request.EnableBuffering();
                string jsonString = String.Empty;

                context.Request.Body.Position = 0;
                using (var inputStream = new StreamReader(context.Request.Body))
                {
                    jsonString = await inputStream.ReadToEndAsync();
                }
                data = JsonNode.Parse(jsonString)?.AsObject() ?? new JsonObject();
            }
            catch (Exception e)
            {
                result.Errors.Add(new RouteError(RouteErrorCode.UnknowError, e));
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
        protected void AddFileToResult(string? propPath, object result)
        {
            foreach (KeyValuePair<string, HttpFile> fileStored in files)
            {
                if (propPath == null || fileStored.Key.StartsWith(propPath))
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

                        PropertyInfo? propertyInfoGet = current?.GetType().GetProperty(s);
                        if (propertyInfoGet != null)
                        {
                            fct = propertyInfoGet.GetValue;
                            typeFieldGet = propertyInfoGet.PropertyType;
                            setTemp = propertyInfoGet.SetValue;
                        }
                        else
                        {
                            FieldInfo? fieldInfoGet = current?.GetType().GetField(s);
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
                    PropertyInfo? propertyInfoSet = current?.GetType().GetProperty(last);
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
                        FieldInfo? fieldInfoSet = current?.GetType().GetField(last);
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
        /// <returns></returns>
        public ResultWithRouteError<object> GetData(Type type, string? propPath = null)
        {
            ResultWithRouteError<object> result = new();

            try
            {
                JsonNode? dataToUse = data;
                if (propPath != null)
                {
                    string[] props = propPath.Split(".");
                    foreach (string prop in props)
                    {
                        if (!string.IsNullOrEmpty(prop))
                        {
                            if (dataToUse is JsonObject obj && obj.TryGetPropertyValue(prop, out var next))
                            {
                                dataToUse = next;
                            }
                            else if (dataToUse is JsonArray arr && int.TryParse(prop, out int index) && index < arr.Count)
                            {
                                dataToUse = arr[index];
                            }
                            else
                            {
                                result.Errors.Add(new RouteError(RouteErrorCode.CantGetValueFromBody, $"Can't find path '{propPath}' in your http body"));
                                return result;
                            }
                        }
                    }
                }

                if (dataToUse != null)
                {
                    var jsonString = dataToUse.ToJsonString();
                    object? temp = JsonSerializer.Deserialize(jsonString, type, RouterMiddleware.config.JSONSettings);
                    if (temp != null)
                    {
                        AddFileToResult(propPath, temp);
                        result.Result = temp;
                    }
                }

            }
            catch (Exception e)
            {
                result.Errors.Add(new RouteError(RouteErrorCode.UnknowError, e));
            }
            return result;
        }

    }

    public class HttpFile
    {
        public string FormName { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string Type { get; set; }

        /// <summary>
        /// Only use during the upload process
        /// </summary>
        internal FileStream? stream;
        public HttpFile(string formName, string filename, string filepath, string type)
        {
            FormName = formName;
            FileName = filename;
            FilePath = filepath;
            Type = type;
        }
        internal HttpFile(string formName, string filename, string filepath, string type, FileStream stream)
        {
            FormName = formName;
            FileName = filename;
            FilePath = filepath;
            Type = type;
            this.stream = stream;
        }

        public bool IsInsideTemp
        {
            get
            {
                return FilePath.StartsWith(RouterMiddleware.config.FileUploadTempDir);
            }
        }

        public bool Move(string path)
        {
            ResultWithRouteError<bool> result = MoveWithError(path);
            return result.Success && result.Result;
        }
        public ResultWithRouteError<bool> MoveWithError(string path)
        {
            ResultWithRouteError<bool> result = new ResultWithRouteError<bool>();
            try
            {
                string? dirPath = Path.GetDirectoryName(path);
                if (dirPath != null)
                    Directory.CreateDirectory(dirPath);

                File.Move(FilePath, path, true);
                result.Result = true;
                FilePath = path;
            }
            catch (Exception e)
            {
                result.Errors.Add(new RouteError(RouteErrorCode.CantMoveFile, e));
            }
            return result;
        }

        public bool Copy(string path)
        {
            ResultWithRouteError<bool> result = CopyWithError(path);
            return result.Success && result.Result;
        }
        public ResultWithRouteError<bool> CopyWithError(string path)
        {
            ResultWithRouteError<bool> result = new ResultWithRouteError<bool>();
            try
            {
                string? dirPath = Path.GetDirectoryName(path);
                if (dirPath != null)
                    Directory.CreateDirectory(dirPath);

                File.Copy(FilePath, path, true);
                result.Result = true;
                FilePath = path;
            }
            catch (Exception e)
            {
                result.Errors.Add(new RouteError(RouteErrorCode.CantMoveFile, e));
            }
            return result;
        }
    }
}
