using AventusSharp.Data.Manager;
using AventusSharp.Data.Manager.DB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using AventusSharp.Tools;
using System.Threading.Tasks;
using AventusSharp.Data.Storage.Default;
using AventusSharp.Data.Attributes;
using AventusSharp.Data.Migrations;
using Microsoft.Extensions.Logging;

namespace AventusSharp.Data
{
    public class DataManagerConfig
    {
        /// <summary>
        /// Define the default Storage connexion to use for your DatabaseDM
        /// </summary>
        public IDBStorage? defaultStorage;
        /// <summary>
        /// Define the default DataManager to use for you Storable
        /// </summary>
        public Type defaultDM = typeof(SimpleDatabaseDM<>);
        /// <summary>
        /// Define logs you need to see
        /// </summary>
        public DataManagerConfigLog log = new();
        /// <summary>
        /// Define if the database fields are nullable by default
        /// </summary>
        public bool nullByDefault = false;
        /// <summary>
        /// Define if local cache must be used. Local cache keep all objects inside the RAM
        /// </summary>
        public bool preferLocalCache = false;
        /// <summary>
        /// Define if element link must only load Type and Id
        /// </summary>
        public bool preferShortLink = false;
        /// <summary>
        /// By default only abstract inheritance is allowed. You can bypass this by setting allowNonAbstractExtension to true
        /// </summary>
        public bool allowNonAbstractExtension = false;
        /// <summary>
        /// Define the name of sql table based on the current type
        /// </summary>
        public Func<Type, string> GetSQLTableName = (type) =>
        {
            Attribute? attr = type.GetCustomAttribute(typeof(SqlName));
            if (attr != null)
            {
                return ((SqlName)attr).Name;
            }
            return type.Name.Split('`')[0];
        };

        /// <summary>
        /// Define config for migration
        /// </summary>
        public DataManagerConfigMigration Migration = new();

        /// <summary>
        /// Define if Aventus have to create models instead of using migrations
        /// </summary>
        public bool AutoCreateModel { get; set; } = true;
        /// <summary>
        /// Define if links must be auto read by default
        /// </summary>
        public bool AutoReadLink { get; set; }
        /// <summary>
        /// Define if links must be auto read by default
        /// </summary>
        public bool AutoCreateLink { get; set; }
        /// <summary>
        /// Define if links must be auto read by default
        /// </summary>
        public bool AutoUpdateLink { get; set; }
        /// <summary>
        /// Define if links must be auto read by default
        /// </summary>
        public bool AutoDeleteLink { get; set; }

        public DataManagerConfig()
        {
        }
    }
    public class DataManagerConfigLog
    {
        /// <summary>
        /// Display dependencies for all your models
        /// </summary>
        public bool monitorDataDependencies = false;
        /// <summary>
        /// Show Analyze managers step
        /// </summary>
        public bool monitorManagerAnalyze = false;
        /// <summary>
        /// Display time to init each data manager
        /// </summary>
        public bool monitorManagerInit = false;
        /// <summary>
        ///  Display time to order each data manager
        /// </summary>
        public bool monitorManagerOrdering = false;
        /// <summary>
        ///  Display ordered managers
        /// </summary>
        public bool monitorManagerOrdered = false;
        /// <summary>
        ///  Display time to order each model
        /// </summary>
        public bool monitorDataOrdering = false;
        /// <summary>
        /// Display ordered models
        /// </summary>
        public bool monitorDataOrdered = false;
        /// <summary>
        /// Print all errors from storable action in the console
        /// </summary>
        public bool printErrorInConsole = false;
    }

    public class DataManagerConfigMigration
    {
        /// <summary>
        /// Define if migrations will be applied though multiple prodivers (for example : 2 databases)
        /// </summary>
        public bool MultipleProviders = false;
    }

    public static class DataMainManager
    {
        static internal DataManagerConfig Config { get; private set; } = new DataManagerConfig();
        private static Action<DataManagerConfig> configureAction = (config) => { };
        private static bool registerDone = false;
        static internal Type? DefaultDMType { get; private set; }
        static internal readonly List<Assembly> searchingAssemblies = new();


        private static VoidWithDataError DefineTypeDM(Type type)
        {
            VoidWithDataError result = new VoidWithDataError();
            if (type != null)
            {
                if (type.IsGenericType)
                {
                    type = type.GetGenericTypeDefinition();
                    if (type.GetGenericArguments().Length == 1)
                    {
                        Type[] constraints = type.GetGenericArguments()[0].GetGenericParameterConstraints();
                        if (constraints.Length > 1 || constraints.Length == 0 || constraints[0] != typeof(IStorable))
                        {
                            result.Errors.Add(new DataError(DataErrorCode.DefaultDMGenericType, "Default DM (" + type.Name + ") must have only one generic type constraint of type IStorable"));
                        }
                        else
                        {
                            DefaultDMType = type;
                        }
                    }
                    else
                    {
                        result.Errors.Add(new DataError(DataErrorCode.DefaultDMGenericType, "Default DM (" + type.Name + ") must have only one generic type"));
                    }
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.DefaultDMGenericType, "Default DM (" + type.Name + ") must be generic with one generic type"));
                }
            }
            else
            {
                result.Errors.Add(new DataError(DataErrorCode.DefaultDMGenericType, "The type defined is null"));
            }
            return result;
        }
        public static void Configure(Action<DataManagerConfig> config)
        {
            configureAction = config;
        }

        public static Task<VoidWithError> Init()
        {
            List<Assembly?> searchingAssemblies = [Assembly.GetEntryAssembly()];
            return Init(searchingAssemblies);
        }
        public static Task<VoidWithError> Init(Assembly? assembly)
        {
            List<Assembly?> searchingAssemblies = [assembly];
            return Init(searchingAssemblies);
        }

        public static async Task<VoidWithError> Init(List<Assembly?> assemblies)
        {
            List<Assembly> assembliesFilter = new();
            foreach (Assembly? assembly in assemblies)
            {
                if (assembly is not null)
                {
                    assembliesFilter.Add(assembly);
                }
            }
            if (!registerDone)
            {
                configureAction(Config);
                VoidWithError resultTemp = DefineTypeDM(Config.defaultDM).ToGeneric();
                if (!resultTemp.Success)
                {
                    return resultTemp;
                }
                registerDone = true;
            }

            if (MergeAssemblies(assembliesFilter) == 0)
            {
                return new VoidWithError();
            }
            return await new DataInit().Init();
        }

        private static int MergeAssemblies(List<Assembly> assemblies)
        {
            int newDlls = 0;
            Assembly? baseAssembly = Assembly.GetAssembly(typeof(DataInit));
            if (baseAssembly != null && !searchingAssemblies.Contains(baseAssembly))
            {
                newDlls++;
                searchingAssemblies.Insert(0, baseAssembly);
            }

            foreach (Assembly assembly in assemblies)
            {
                if (!searchingAssemblies.Contains(assembly))
                {
                    newDlls++;
                    searchingAssemblies.Add(assembly);
                }
            }
            return newDlls;
        }

        private class DataInit
        {
            private readonly DataManagerConfig config = DataMainManager.Config;
            private List<ManagerInformation> managerInformations = new();
            private readonly Dictionary<Type, DataInformation> dataInformations = new();
            private List<DataInformation> orderedData = new();
            private List<ManagerInformation> orderedManager = new();


            public DataInit()
            {

            }


            public async Task<VoidWithError> Init()
            {
                VoidWithError resultTemp = new VoidWithError();
                try
                {
                    resultTemp = GetAllManagers().ToGeneric();
                    if (!resultTemp.Success)
                    {
                        return resultTemp;
                    }
                    resultTemp = CalculateDataDependencies().ToGeneric();
                    if (!resultTemp.Success)
                    {
                        return resultTemp;
                    }
                    resultTemp = OrderData().ToGeneric();
                    if (!resultTemp.Success)
                    {
                        return resultTemp;
                    }
                    resultTemp = MergeManager().ToGeneric();
                    if (!resultTemp.Success)
                    {
                        return resultTemp;
                    }
                    resultTemp = OrderedManager().ToGeneric();
                    if (!resultTemp.Success)
                    {
                        return resultTemp;
                    }
                    resultTemp = await InitManager();
                    if (!resultTemp.Success)
                    {
                        return resultTemp;
                    }
                }
                catch (Exception e)
                {
                    resultTemp.Errors.Add(new DataError(DataErrorCode.UnknownError, e));
                }
                return resultTemp;
            }

            private VoidWithDataError GetAllManagers()
            {

                VoidWithDataError result = new VoidWithDataError();
                Dictionary<Type, ManagerInformation> managerInformations = new();
                bool monitor = this.config.log.monitorManagerAnalyze;
                if (monitor)
                {
                    AventusLogger.Instance.LogInformation("*********** Analyze managers **********");
                }
                Stopwatch? time = null;
                List<Type> managerTypes = GenericDM.GetExistingDMTypes();

                foreach (Assembly assembly in searchingAssemblies)
                {
                    Type[] types = assembly.GetTypes();
                    foreach (Type type in types)
                    {
                        if (type.GetInterfaces().Contains(typeof(IGenericDM)) && !type.IsAbstract && !type.IsGenericTypeDefinition && !managerTypes.Contains(type))
                        {
                            managerTypes.Add(type);
                        }
                    }
                }

                void addDependencies(Type typeFrom, Type typeDependency, string name)
                {
                    if (!managerInformations[typeFrom].dependencies.ContainsKey(typeDependency))
                    {
                        managerInformations[typeFrom].dependencies[typeDependency] = new List<string>();
                    }
                    if (!managerInformations[typeFrom].dependencies[typeDependency].Contains(name))
                    {
                        managerInformations[typeFrom].dependencies[typeDependency].Add(name);
                    }
                }


                foreach (Type managerType in managerTypes)
                {
                    if (monitor)
                    {
                        time = new Stopwatch();
                        time.Restart();
                    }

                    MethodInfo? GetInstance = managerType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                    if (GetInstance == null)
                    {
                        result.Errors.Add(new DataError(DataErrorCode.MethodNotFound, "Manager " + managerType.Name + " doesn't have a GetInstance function"));
                        return result;
                    }
                    IGenericDM? manager = (IGenericDM?)GetInstance.Invoke(null, null);
                    if (manager == null)
                    {
                        continue;
                    }
                    ManagerInformation info = new(manager)
                    {
                        dependencies = new Dictionary<Type, List<string>>(),
                    };

                    info.AddDataInformation(new DataInformation()
                    {
                        Data = manager.GetMainType(),
                        dependencies = new Dictionary<Type, List<string>>(),
                        membersInfo = new Dictionary<string, DataMemberInfo>()

                    });
                    managerInformations.Add(managerType, info);
                    if (monitor)
                    {
                        AventusLogger.Instance.LogInformation(managerType.Name);
                    }


                    List<Type> manualDependencies = manager.DefineManualDependencies();
                    foreach (Type manualDependency in manualDependencies)
                    {
                        if (manualDependency.GetInterfaces().Contains(typeof(IStorable)))
                        {
                            addDependencies(managerType, manualDependency, "*manual");
                        }
                        else
                        {
                            new DataError(DataErrorCode.TypeNotStorable, "type " + manualDependency.Name + " is not storable, so you can't add it inside manual dependencies of manager " + manager.Name).Print();
                        }
                    }

                }


                this.managerInformations = managerInformations.Values.ToList();
                return result;
            }
            private void AddDataDependency(Type typeFrom, Type? typeDependency, string name)
            {
                if (typeDependency == null || typeDependency == typeof(Object))
                {
                    return;
                }
                if (typeFrom.IsGenericType)
                {
                    typeFrom = typeFrom.GetGenericTypeDefinition();
                }
                if (typeDependency.IsGenericType)
                {
                    if (!typeDependency.IsGenericTypeDefinition)
                    {
                        foreach (Type type in typeDependency.GenericTypeArguments)
                        {
                            // TODO check how to prevent infinite loop
                            AddDataDependency(typeFrom, type, name);
                        }
                    }
                    typeDependency = typeDependency.GetGenericTypeDefinition();
                }

                if (!dataInformations[typeFrom].dependencies.ContainsKey(typeDependency))
                {
                    dataInformations[typeFrom].dependencies[typeDependency] = new List<string>();
                }
                if (!dataInformations[typeFrom].dependencies[typeDependency].Contains(name))
                {
                    dataInformations[typeFrom].dependencies[typeDependency].Add(name);
                }
            }
            private VoidWithDataError CalculateDataDependencies()
            {
                VoidWithDataError result = new();
                bool monitor = this.config.log.monitorDataDependencies;
                if (monitor)
                {
                    AventusLogger.Instance.LogInformation("*********** Calculate data dependencies **********");
                }
                List<Type> dataTypes = new();
                foreach (Assembly assembly in searchingAssemblies)
                {
                    dataTypes.AddRange(assembly.GetTypes().Where(type => type.GetInterfaces().Contains(typeof(IStorable)) && (type.IsClass || type.IsInterface) && type.GetCustomAttribute<ManualInit>() == null).ToList());
                }
                dataTypes.Insert(0, typeof(IStorable));

                foreach (Type dataType in dataTypes)
                {
                    result = CalculateDataDependency(dataType);
                    if (!result.Success)
                    {
                        return result;
                    }
                }

                return result;
            }
            private VoidWithDataError CalculateDataDependency(Type dataType)
            {
                VoidWithDataError result = new();
                if (dataInformations.ContainsKey(dataType))
                {
                    return result;
                }
                DataInformation info = new()
                {
                    Data = dataType,
                    dependencies = new Dictionary<Type, List<string>>(),
                    membersInfo = new Dictionary<string, DataMemberInfo>()

                };
                dataInformations.Add(dataType, info);

                // set Data info inside Manager info
                ResultWithDataError<ManagerInformation> managerInfoResult = GetManager(info);
                if (!managerInfoResult.Success || managerInfoResult.Result == null)
                {
                    result.Errors = managerInfoResult.Errors;
                    return result;
                }
                ManagerInformation managerInfo = managerInfoResult.Result;
                managerInfo.AddDataInformation(info);
                info.ManagerInfo = managerInfo;
                Type? interfaceType = null;
                if (dataType.IsGenericType)
                {
                    if (!dataType.IsAbstract && !dataType.IsInterface)
                    {
                        result.Errors.Add(new DataError(DataErrorCode.GenericNotAbstract, "You class generic " + info.Name + " must be set as abstract"));
                        return result;
                    }
                    ResultWithDataError<Type> interfaceTypeTemp = GetTypeForGenericClass(dataType);
                    if (interfaceTypeTemp.Success && interfaceTypeTemp.Result != null)
                    {
                        interfaceType = interfaceTypeTemp.Result;
                        AddDataDependency(dataType, interfaceType, "*constraint");
                        if (dataInformations.ContainsKey(interfaceType))
                        {
                            dataInformations[interfaceType].GenericType = dataType;
                        }
                        else
                        {
                            VoidWithDataError resultTemp = CalculateDataDependency(interfaceType);
                            if (!resultTemp.Success)
                            {
                                result.Errors.AddRange(resultTemp.Errors);
                                return result;
                            }
                            dataInformations[interfaceType].GenericType = dataType;
                        }
                    }
                }
                else if (dataType.IsAbstract && dataType.IsClass)
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknownError, "How did you do that for " + dataType.Name));
                    return result;
                }

                // load parent
                Type? parentType = dataType.BaseType;
                if (parentType == null || parentType == typeof(Object)) { }
                else if (parentType.IsInterface)
                {
                    AddDataDependency(dataType, parentType, "*parent");
                }
                else if (parentType.IsAbstract && parentType.IsGenericType)
                {
                    AddDataDependency(dataType, parentType, "*parent");
                }
                else if (config.allowNonAbstractExtension && !parentType.IsAbstract && !parentType.IsGenericType)
                {
                    AddDataDependency(dataType, parentType, "*parent");
                }
                else
                {
                    result.Errors.Add(new DataError(DataErrorCode.ParentNotAbstract, "A parent must be abstract and generic " + parentType.Name));
                    return result;
                }

                //load current interface
                Type[] interfaces = dataType.GetCurrentInterfaces();
                foreach (Type _interface in interfaces)
                {
                    if (_interface.GetInterfaces().Contains(typeof(IStorable)))
                    {
                        AddDataDependency(dataType, _interface, "*interface");
                    }
                }

                if (interfaceType != null)
                {
                    dataType = interfaceType;
                }

                // load fields
                FieldInfo[] fields = dataType.GetFields();
                foreach (FieldInfo field in fields)
                {
                    DataMemberInfo memberInfo = new(field);
                    if (!TypeTools.IsPrimitiveType(memberInfo.Type) && memberInfo.GetAttribute<IAvoidDependency>(false) == null)
                    {
                        AddDataDependency(dataType, memberInfo.Type, memberInfo.Name);
                    }
                    else
                    {
                        ForeignKey? foreignKeyAttr = memberInfo.GetAttribute<ForeignKey>(false);
                        if (foreignKeyAttr != null)
                        {
                            AddDataDependency(dataType, foreignKeyAttr.Type, memberInfo.Name);
                        }
                    }
                    info.membersInfo.Add(memberInfo.Name, memberInfo);
                }
                PropertyInfo[] properties = dataType.GetProperties(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance);
                foreach (PropertyInfo property in properties)
                {
                    DataMemberInfo memberInfo = new(property);
                    ForeignKey? foreignKeyAttr = memberInfo.GetAttribute<ForeignKey>(false);
                    if (foreignKeyAttr != null)
                    {
                        AddDataDependency(dataType, foreignKeyAttr.Type, memberInfo.Name);
                    }
                    else if (!TypeTools.IsPrimitiveType(memberInfo.Type) && memberInfo.GetAttribute<IAvoidDependency>(false) == null)
                    {
                        AddDataDependency(dataType, memberInfo.Type, memberInfo.Name);
                    }

                    info.membersInfo.Add(memberInfo.Name, memberInfo);
                }

                if (config.log.monitorDataDependencies)
                {
                    AventusLogger.Instance.LogInformation(info.ToString());
                }
                return result;
            }
            private VoidWithDataError OrderData()
            {
                VoidWithDataError result = new VoidWithDataError();
                bool monitor = config.log.monitorDataOrdering;
                List<DataInformation> infos = dataInformations.Values.ToList();
                Stopwatch? time = null;
                orderedData = new List<DataInformation>();
                if (monitor)
                {
                    AventusLogger.Instance.LogInformation("*********** Data ordering **********");
                }
                foreach (DataInformation info in infos)
                {
                    if (monitor)
                    {
                        time = new Stopwatch();
                        time.Restart();
                    }
                    ResultWithDataError<int> resultTemp = OrderDataLoop(info);
                    if (!resultTemp.Success)
                    {
                        result.Errors.AddRange(resultTemp.Errors);
                        return result;
                    }
                    if (monitor && time != null)
                    {
                        AventusLogger.Instance.LogInformation("Data " + info.Name + " ordering loop in " + time.ElapsedMilliseconds + "ms");
                        time.Stop();
                        time = null;
                    }
                }
                if (config.log.monitorDataOrdered)
                {
                    AventusLogger.Instance.LogInformation("*********** Data ordered **********");
                    int i = 1;
                    foreach (DataInformation dataInfo in orderedData)
                    {
                        AventusLogger.Instance.LogInformation(i + ". " + dataInfo.Name + " - " + dataInfo.ManagerInfo.Manager.Name);
                        i++;
                    }
                }

                ForceIStorableFirst();
                return result;
            }
            private void ForceIStorableFirst()
            {
                orderedData.Remove(dataInformations[typeof(IStorable)]);
                orderedData.Insert(0, dataInformations[typeof(IStorable)]);
            }


            private ResultWithDataError<int> OrderDataLoop(DataInformation dataInformation, List<DataInformation>? waitingData = null)
            {
                ResultWithDataError<int> result = new ResultWithDataError<int>();
                waitingData ??= new List<DataInformation>();

                if (waitingData.Contains(dataInformation))
                {
                    if (waitingData[^1] == dataInformation)
                    {
                        // self referencing no error needed
                        result.Result = -1;
                        return result;
                    }
                    string msgError = "Infinite loop found for Data " + dataInformation.Name + "\n";
                    msgError += "Elements in loop : \n\t - " + string.Join("\n\t - ", waitingData.Select(d => d.Name)) + "\n\t - " + dataInformation.Name;
                    result.Errors.Add(new DataError(DataErrorCode.InfiniteLoop, msgError));
                    return result;
                }

                try
                {
                    int indexOfRam = orderedData.IndexOf(dataInformation);
                    if (indexOfRam != -1)
                    {
                        result.Result = indexOfRam + 1;
                        return result;
                    }
                    int insertIndex = 0;

                    waitingData.Add(dataInformation);

                    List<Type> dependencies = dataInformation.dependencies.Keys.ToList();
                    foreach (Type dependencyType in dependencies)
                    {
                        if (dataInformations.ContainsKey(dependencyType))
                        {
                            ResultWithDataError<int> insertIndexTemp = OrderDataLoop(dataInformations[dependencyType], waitingData);
                            if (!insertIndexTemp.Success)
                            {
                                // its an error
                                return insertIndexTemp;
                            }
                            if (insertIndexTemp.Result >= 0 && insertIndexTemp.Result > insertIndex)
                            {
                                insertIndex = insertIndexTemp.Result;
                            }
                        }
                    }
                    waitingData.Remove(dataInformation);

                    if (orderedData.Contains(dataInformation))
                    {
                        result.Errors.Add(new DataError(DataErrorCode.SelfReferecingDependency, "Self referencing dependencies found for Data " + dataInformation.Name));
                        return result;
                    }
                    orderedData.Insert(insertIndex, dataInformation);

                }
                catch (Exception e)
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknownError, e));
                }
                result.Result = orderedData.Count;
                return result;
            }
            private VoidWithDataError MergeManager()
            {
                VoidWithDataError result = new VoidWithDataError();
                // merge manager
                for (int i = orderedData.Count - 1; i > 0; i--)
                {
                    DataInformation dataInformationToMerge = orderedData.ElementAt(i);
                    for (int j = i - 1; j >= 0; j--)
                    {
                        DataInformation dataInformationToCheck = orderedData.ElementAt(j);
                        if (dataInformationToCheck.Data.IsInterface)
                        {
                            if (dataInformationToMerge.Data.GetInterfaces().Contains(dataInformationToCheck.Data))
                            {
                                if (dataInformationToMerge.ManagerInfo != dataInformationToCheck.ManagerInfo)
                                {
                                    if (dataInformationToCheck.IsMergeable(this.dataInformations))
                                    {
                                        // do not merge if dummy type => prevent real type to be override by dummy
                                        if (!dataInformationToCheck.ManagerInfo.IsDummy || dataInformationToMerge.ManagerInfo.IsDummy)
                                        {
                                            ManagerInformation oldManager = dataInformationToMerge.ManagerInfo;
                                            dataInformationToMerge.ManagerInfo = dataInformationToCheck.ManagerInfo;
                                            dataInformationToMerge.ManagerInfo.AddDataInformation(dataInformationToMerge);

                                            oldManager.RemoveDataInformation(dataInformationToMerge);
                                            if (!oldManager.IsUseful)
                                            {
                                                managerInformations.Remove(oldManager);
                                            }
                                        }
                                    }


                                }
                            }
                        }

                    }
                }

                // merge dependencies and order data inside managers
                for (int i = 0; i < managerInformations.Count; i++)
                {
                    managerInformations[i].PrepareOrderDataInformation();

                }
                foreach (DataInformation dataInformation in orderedData)
                {
                    dataInformation.ManagerInfo.AddDataInformation(dataInformation);
                    foreach (KeyValuePair<Type, List<string>> dependencies in dataInformation.dependencies)
                    {
                        Type dependencyType = dependencies.Key;
                        List<string> dependencyPaths = dependencies.Value;
                        if (!dataInformation.ManagerInfo.UsedForType(dependencyType))
                        {
                            if (!dataInformation.ManagerInfo.dependencies.ContainsKey(dependencyType))
                            {
                                dataInformation.ManagerInfo.dependencies.Add(dependencyType, new List<string>());
                            }
                            foreach (string dependencyPath in dependencyPaths)
                            {
                                if (!dataInformation.ManagerInfo.dependencies[dependencyType].Contains(dependencyPath))
                                {
                                    dataInformation.ManagerInfo.dependencies[dependencyType].Add(dependencyPath);
                                }
                            }
                        }
                    }
                }

                return result;
            }
            private VoidWithDataError OrderedManager()
            {
                VoidWithDataError result = new();
                bool monitor = this.config.log.monitorManagerOrdering;
                Stopwatch? time = null;
                orderedManager = new List<ManagerInformation>();
                if (monitor)
                {
                    AventusLogger.Instance.LogInformation("*********** Manager ordering **********");
                }
                foreach (ManagerInformation info in managerInformations)
                {
                    if (monitor)
                    {
                        time = new Stopwatch();
                        time.Restart();
                    }
                    ResultWithDataError<int> resultTemp = OrderManagerLoop(info);
                    if (!resultTemp.Success)
                    {
                        result.Errors.AddRange(resultTemp.Errors);
                        return result;
                    }
                    if (time != null)
                    {
                        AventusLogger.Instance.LogInformation("Manager " + info.Manager.Name + " ordering loop in " + time.ElapsedMilliseconds + "ms");
                        time.Stop();
                        time = null;
                    }
                }
                if (config.log.monitorManagerOrdered)
                {
                    AventusLogger.Instance.LogInformation("*********** Manager ordered **********");
                    int i = 1;
                    foreach (ManagerInformation managerInfo in orderedManager)
                    {
                        AventusLogger.Instance.LogInformation(i + ". " + managerInfo.Manager.Name);
                        i++;
                    }
                }
                return result;
            }
            private ResultWithDataError<int> OrderManagerLoop(ManagerInformation managerInformation, List<ManagerInformation>? waitingData = null)
            {
                ResultWithDataError<int> result = new();
                waitingData ??= new List<ManagerInformation>();

                if (waitingData.Contains(managerInformation))
                {
                    if (waitingData[^1] == managerInformation)
                    {
                        // self referencing no error needed
                        result.Result = -1;
                        return result;
                    }
                    string msgError = "Infinite loop found for Manager " + managerInformation.Manager.Name + "\n";
                    msgError += "Elements in loop : \n\t- " + string.Join("\n\t- ", waitingData.Select(d => d.Manager.Name)) + "\n\t- " + managerInformation.Manager.Name;
                    result.Errors.Add(new DataError(DataErrorCode.InfiniteLoop, msgError));
                    return result;
                }

                try
                {
                    int indexOfRam = orderedManager.IndexOf(managerInformation);
                    if (indexOfRam != -1)
                    {
                        result.Result = indexOfRam + 1;
                        return result;
                    }
                    int insertIndex = 0;

                    waitingData.Add(managerInformation);

                    List<Type> dependencies = managerInformation.dependencies.Keys.ToList();
                    foreach (Type dependencyType in dependencies)
                    {
                        if (dataInformations.ContainsKey(dependencyType))
                        {
                            ManagerInformation? managerInfo = dataInformations[dependencyType].ManagerInfo;
                            if (managerInfo != null)
                            {
                                ResultWithDataError<int> insertIndexTemp = OrderManagerLoop(managerInfo, waitingData);
                                if (!insertIndexTemp.Success)
                                {
                                    // its an error
                                    return insertIndexTemp;
                                }
                                if (insertIndexTemp.Result >= 0 && insertIndexTemp.Result > insertIndex)
                                {
                                    insertIndex = insertIndexTemp.Result;
                                }
                            }
                        }
                    }
                    waitingData.Remove(managerInformation);

                    if (orderedManager.Contains(managerInformation))
                    {
                        result.Errors.Add(new DataError(DataErrorCode.SelfReferecingDependency, "Self referencing dependency found for Manager " + managerInformation.Manager.Name));
                        return result;
                    }
                    orderedManager.Insert(insertIndex, managerInformation);

                }
                catch (Exception e)
                {
                    result.Errors.Add(new DataError(DataErrorCode.UnknownError, e));
                }
                result.Result = orderedManager.Count;
                return result;
            }
            private async Task<VoidWithError> InitManager()
            {
                VoidWithError result = new VoidWithError();
                bool monitor = this.config.log.monitorManagerInit;
                if (monitor)
                {
                    AventusLogger.Instance.LogInformation("*********** Init managers **********");
                }
                Stopwatch? time = null;
                List<IGenericDM> genericDMs = new();
                foreach (ManagerInformation managerInformation in orderedManager)
                {
                    if (!managerInformation.CanBeInit(false))
                    {
                        continue;
                    }
                    IGenericDM manager = managerInformation.Manager;
                    if (!genericDMs.Contains(manager))
                    {
                        genericDMs.Add(manager);
                        ResultWithDataError<PyramidInfo> pyramidResult = CreatePyramid(managerInformation);
                        if (!pyramidResult.Success || pyramidResult.Result == null)
                        {
                            result.Errors.AddRange(pyramidResult.Errors);
                            return result;
                        }
                        VoidWithError resultTemp = await manager.SetConfiguration(pyramidResult.Result, this.config);
                        if (!resultTemp.Success)
                        {
                            result.Errors.AddRange(resultTemp.Errors);
                            return result;
                        }
                    }
                }

                await result.RunAsync(() => MigrationManager.Run(searchingAssemblies));

                foreach (IGenericDM dm in genericDMs)
                {
                    if (monitor)
                    {
                        time = new Stopwatch();
                        time.Restart();
                    }
                    VoidWithError resultTemp = await dm.Init();
                    if (!resultTemp.Success)
                    {
                        result.Errors.AddRange(resultTemp.Errors);
                        return result;
                    }
                    if (time != null)
                    {
                        AventusLogger.Instance.LogInformation("Manager " + dm.Name + " init in " + time.ElapsedMilliseconds + "ms");
                        time.Stop();
                        time = null;
                    }
                }
                return result;
            }

            private ResultWithDataError<PyramidInfo> CreatePyramidStep(DataInformation dataInformation, Dictionary<Type, PyramidInfo> pyramidFloors, Dictionary<Type, string> aliasUsed)
            {
                ResultWithDataError<PyramidInfo> result = new();
                if (!dataInformation.Data.IsInterface)
                {
                    Type? constraint = null;
                    Type? parent = null;
                    foreach (KeyValuePair<Type, List<string>> pair in dataInformation.dependencies)
                    {
                        if (pair.Value.Contains("*constraint"))
                        {
                            constraint = pair.Key;
                            if (!aliasUsed.ContainsKey(constraint))
                            {
                                aliasUsed[constraint] = dataInformation.Name;
                            }
                            else
                            {
                                string msgError = "You can't use the constraint " + constraint.Name + " inside class " + dataInformation.Name + " because interface is still referencing by " + aliasUsed[constraint] + ". You must create an interface for this abstract type.";
                                result.Errors.Add(new DataError(DataErrorCode.InterfaceNotUnique, msgError));
                                return result;
                            }
                        }
                        else if (pair.Value.Contains("*parent"))
                        {
                            parent = pair.Key;
                        }
                    }
                    PyramidInfo info = new(
                        type: dataInformation.Data,
                        memberInfo: dataInformation.membersInfo.Values.ToList()
                    )
                    {
                        dependencies = dataInformation.dependencies,
                        aliasType = constraint,
                        isForceInherit = dataInformation.IsForceInherit
                    };
                    result.Result = info;
                    if (parent != null)
                    {
                        if (pyramidFloors.ContainsKey(parent))
                        {
                            info.parent = pyramidFloors[parent];
                            info.parent.children.Add(info);
                        }
                        else if (dataInformations.ContainsKey(parent) && dataInformations[parent].IsForceInherit)
                        {
                            result = CreatePyramidStep(dataInformations[parent], pyramidFloors, aliasUsed);
                            if (result.Success && result.Result != null)
                            {
                                if (pyramidFloors.ContainsKey(parent))
                                {
                                    info.parent = pyramidFloors[parent];
                                    info.parent.children.Add(info);
                                }
                                else
                                {
                                    result.Errors.Add(new DataError(DataErrorCode.UnknownError, "Somthing went wrong when creating pyramid but I don't know why"));
                                    return result;
                                }
                            }
                            else if (result.Errors.Count > 0)
                            {
                                return result;
                            }
                        }
                        else if (config.allowNonAbstractExtension && dataInformations.ContainsKey(parent))
                        {
                            result = CreatePyramidStep(dataInformations[parent], pyramidFloors, aliasUsed);
                            if (result.Success && result.Result != null)
                            {
                                if (pyramidFloors.ContainsKey(parent))
                                {
                                    info.parent = pyramidFloors[parent];
                                    info.parent.nonGenericExtension = true;
                                    info.parent.children.Add(info);
                                }
                                else
                                {
                                    result.Errors.Add(new DataError(DataErrorCode.UnknownError, "Somthing went wrong when creating pyramid but I don't know why"));
                                    return result;
                                }
                            }
                            else if (result.Errors.Count > 0)
                            {
                                return result;
                            }
                        }
                        else
                        {
                            result.Errors.Add(new DataError(DataErrorCode.UnknownError, "Somthing went wrong when creating pyramid but I don't know why"));
                            return result;
                        }
                    }
                    pyramidFloors[dataInformation.Data] = info;
                    return result;
                }
                return result;
            }
            private ResultWithDataError<PyramidInfo> CreatePyramid(ManagerInformation managerInformation)
            {
                ResultWithDataError<PyramidInfo> result = new();

                Dictionary<Type, PyramidInfo> pyramidFloors = new();
                Dictionary<Type, string> aliasUsed = new(); // interface type, class name
                // at this point data inforamtion is ordered but it's a mix of class, abstract class and interface
                foreach (DataInformation dataInformation in managerInformation.GetDataInformation())
                {
                    try
                    {
                        ResultWithDataError<PyramidInfo> info = CreatePyramidStep(dataInformation, pyramidFloors, aliasUsed);
                        if (info.Success && info.Result != null)
                        {
                            result.Result ??= info.Result;
                        }
                        else if (info.Errors.Count > 0)
                        {
                            result.Errors.AddRange(info.Errors);
                            return result;
                        }
                    }
                    catch (Exception e)
                    {
                        result.Errors.Add(new DataError(DataErrorCode.UnknownError, e));
                        return result;
                    }
                }
                return result;
            }
            private ResultWithDataError<ManagerInformation> GetManager(DataInformation dataInformation)
            {
                ResultWithDataError<ManagerInformation> result = new();
                try
                {
                    Type? dataType = dataInformation.Data;
                    if (dataType.IsGenericType)
                    {
                        // type must be the interface
                        ResultWithDataError<Type> resultTemp = GetTypeForGenericClass(dataType);
                        if (!resultTemp.Success || resultTemp.Result == null)
                        {
                            result.Errors = resultTemp.Errors;
                            return result;
                        }
                        dataType = resultTemp.Result;
                    }

                    List<ManagerInformation> managers = managerInformations.ToList();
                    foreach (ManagerInformation manager in managers)
                    {
                        if (manager.UsedForType(dataType))
                        {
                            result.Result = manager;
                            return result;
                        }
                    }

                    if (DefaultDMType == null)
                    {
                        result.Errors.Add(new DataError(DataErrorCode.DMNotExist, "You must define a default DM"));
                        return result;
                    }
                    Type simpleType = DefaultDMType.MakeGenericType(new Type[] { dataType });
                    MethodInfo? GetInstance = simpleType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                    if (GetInstance == null)
                    {
                        result.Errors.Add(new DataError(DataErrorCode.MethodNotFound, "Your default DM doesn't have a method GetInstance"));
                        return result;
                    }
                    IGenericDM? simpleManager = (IGenericDM?)GetInstance.Invoke(null, null);
                    if (simpleManager == null)
                    {
                        result.Errors.Add(new DataError(DataErrorCode.DMNotExist, "The methode GetInstance inside your default DM doesn't return a Generic DM"));
                        return result;
                    }
                    ManagerInformation info = new(simpleManager)
                    {
                        dependencies = new Dictionary<Type, List<string>>(),
                        IsDummy = true,
                    };
                    info.AddDataInformation(dataInformation);
                    managerInformations.Add(info);
                    result.Result = info;
                }
                catch (Exception e)
                {
                    result.Errors.Add(new(DataErrorCode.UnknownError, e));
                }
                return result;
            }

            /// <summary>
            /// Always the first generic parameter for an abstract class
            /// </summary>
            /// <param name="dataType"></param>
            /// <returns></returns>
            private ResultWithDataError<Type> GetTypeForGenericClass(Type dataType)
            {
                ResultWithDataError<Type> result = new();
                if (dataType.IsGenericType)
                {
                    Dictionary<string, Type> genericConstraintMapping = new();
                    Type genericType = dataType.GetGenericTypeDefinition();
                    Type[] realValueConstraints = dataType.GetGenericArguments();
                    Type[] genericValueConstraints = genericType.GetGenericArguments();
                    for (int i = 0; i < genericValueConstraints.Length; i++)
                    {
                        genericConstraintMapping.Add(genericValueConstraints[i].ToString(), realValueConstraints[i]);
                    }
                    Type[] def = genericType.GetGenericArguments();
                    Type tp = def[0];
                    Type[] tpConstraints = tp.GetGenericParameterConstraints();
                    List<Type> resultType = new();
                    foreach (Type tpc in tpConstraints)
                    {
                        Type constraintType = tpc;
                        if (tpc.IsInterface)
                        {
                            constraintType = TransformTypeGenericToReal(tpc, genericConstraintMapping);
                        }
                        if (constraintType == typeof(IStorable) || constraintType.GetInterfaces().Contains(typeof(IStorable)))
                        {
                            resultType.Add(constraintType);
                        }
                    }

                    if (resultType.Count == 0)
                    {
                        string msgError = "You need to define a constraint IStorable for the type " + tp.Name + " of the class " + dataType.Name;
                        result.Errors.Add(new DataError(DataErrorCode.TypeNotStorable, msgError));
                        return result;
                    }
                    else if (resultType.Count > 1)
                    {
                        string msgError = "Too many constraints IStorable (" + string.Join(", ", resultType.Select(p => p.Name)) + ") for the type " + tp.Name + " of the class " + dataType.Name;
                        result.Errors.Add(new DataError(DataErrorCode.TypeTooMuchStorable, msgError));
                        return result;
                    }
                    else
                    {
                        result.Result = resultType[0];
                    }
                }
                else
                {
                    result.Result = dataType;
                }
                return result;
            }

            private Type TransformTypeGenericToReal(Type type, Dictionary<string, Type> genericConstraintMapping)
            {
                Type result = type;
                if (type.IsGenericType)
                {

                    Type[] args = type.GetGenericArguments();
                    Type[] typesToUse = new Type[args.Length];
                    for (int i = 0; i < args.Length; i++)
                    {
                        try
                        {
                            if (args[i].IsGenericParameter)
                            {
                                typesToUse[i] = genericConstraintMapping[args[i].ToString()];
                            }
                            else if (args[i].IsGenericType)
                            {
                                typesToUse[i] = TransformTypeGenericToReal(args[i], genericConstraintMapping);
                            }
                            else
                            {
                                typesToUse[i] = args[i];
                            }
                        }
                        catch (Exception e)
                        {
                            new DataError(DataErrorCode.UnknownError, e).Print();
                            new DataError(DataErrorCode.UnknownError, args[i].ToString() + " on constraint " + type.Name + " on type " + type.Name).Print();
                        }
                    }
                    result = type.GetGenericTypeDefinition().MakeGenericType(typesToUse);
                }
                return result;
            }
        }



        public class DataInformation
        {
#pragma warning disable CS8618 // Un champ non-nullable doit contenir une valeur non-null lors de la fermeture du constructeur. Envisagez de déclarer le champ comme nullable.
            public Type Data { get; set; }
#pragma warning restore CS8618 // Un champ non-nullable doit contenir une valeur non-null lors de la fermeture du constructeur. Envisagez de déclarer le champ comme nullable.

            // set if data is interface
            public Type? GenericType { get; set; }
            public string Name
            {
                get
                {
                    return TypeTools.GetReadableName(Data);
                }
            }
            public bool IsMergeable(Dictionary<Type, DataInformation> dataInformations)
            {
                if (Data.IsInterface)
                {
                    if (GenericType != null && dataInformations.ContainsKey(GenericType))
                    {
                        return dataInformations[GenericType].IsMergeable(dataInformations);
                    }
                    return false;
                }
                else
                {
                    if (IsForceInherit)
                    {
                        foreach (KeyValuePair<Type, List<string>> dependency in dependencies)
                        {
                            if (dependency.Value.Contains("*parent") && dataInformations.ContainsKey(dependency.Key))
                            {
                                return dataInformations[dependency.Key].IsMergeable(dataInformations);
                            }
                        }
                        return false;
                    }
                    return true;
                }

            }

            private bool? _IsForceInherit = null;
            public bool IsForceInherit
            {
                get => _IsForceInherit ??= Data.GetCustomAttributes(false).ToList().Exists(a => a is ForceInherit);
            }

#pragma warning disable CS8618 // Un champ non-nullable doit contenir une valeur non-null lors de la fermeture du constructeur. Envisagez de déclarer le champ comme nullable.
            public ManagerInformation ManagerInfo { get; set; }
#pragma warning restore CS8618 // Un champ non-nullable doit contenir une valeur non-null lors de la fermeture du constructeur. Envisagez de déclarer le champ comme nullable.
            public Dictionary<Type, List<string>> dependencies = new();
            public Dictionary<string, DataMemberInfo> membersInfo = new();
            public override string ToString()
            {
                string txt = "**********************\n";
                txt += "Data " + Name + "\n";
                txt += "Has members :\n";
                foreach (KeyValuePair<string, DataMemberInfo> pair in membersInfo)
                {
                    txt += "\t" + pair.Value.ToString() + "\n";
                }
                txt += "Has dependencyTypes with :\n";
                foreach (KeyValuePair<Type, List<string>> pair in dependencies)
                {
                    txt += "\t" + pair.Key.Name + " (" + pair.Key.Assembly.GetName().Name + ")\n";
                    foreach (string path in pair.Value)
                    {
                        txt += "\t\t" + path + "\n";
                    }
                }
                txt += "**********************\n";
                return txt;
            }
        }

        public class ManagerInformation
        {
            public IGenericDM Manager { get; private set; }
            public Dictionary<Type, List<string>> dependencies = new();
            public Dictionary<Type, DataInformation> dataInformations = new();

            public bool IsDummy
            {
                get; set;
            }

            public ManagerInformation(IGenericDM manager)
            {
                this.Manager = manager;
            }

            /**
             * Can be init only if there is at least one class not in force inherit
             */
            public bool CanBeInit(bool printError)
            {
                foreach (DataInformation info in dataInformations.Values)
                {
                    if (!info.Data.IsInterface && !info.IsForceInherit)
                    {
                        return true;
                    }
                }
                if (printError)
                {
                    if (!dataInformations.ContainsKey(typeof(IStorable)) || !dataInformations.ContainsKey(typeof(IStorableTimestamp)) || dataInformations.Count != 2)
                    {
                        string msgError = "Can't init " + Manager.Name + " because only contains ForceInehrit elements";
                        new DataError(DataErrorCode.DMOnlyForceInherit, msgError).Print();
                    }
                }
                return false;
            }

            public void AddDataInformation(DataInformation dataInformation)
            {
                dataInformations[dataInformation.Data] = dataInformation;
            }
            public void RemoveDataInformation(DataInformation dataInformation)
            {
                if (dataInformations.ContainsKey(dataInformation.Data))
                {
                    dataInformations.Remove(dataInformation.Data);
                }
            }

            public void PrepareOrderDataInformation()
            {
                this.dataInformations = new Dictionary<Type, DataInformation>();
            }
            public List<DataInformation> GetDataInformation()
            {
                return dataInformations.Values.ToList();
            }
            public bool IsUseful { get => dataInformations.Count > 0; }
            public bool UsedForType(Type type)
            {
                return dataInformations.ContainsKey(type);
            }
            public string PrintCurrentAssembly()
            {

                string txt = "**********************\n";
                txt += "Manager " + Manager.Name + "\n";
                txt += "Used for types :\n";
                foreach (DataInformation data in dataInformations.Values)
                {
                    txt += "\t" + data.Name + "\n";
                }


                txt += "Has dependencyTypes with :\n";
                foreach (KeyValuePair<Type, List<string>> pair in dependencies)
                {
                    if (pair.Key.Assembly == Assembly.GetEntryAssembly())
                    {
                        txt += "\t" + pair.Key.Name + " (" + pair.Key.Assembly.GetName().Name + ")\n";
                        foreach (string path in pair.Value)
                        {
                            txt += "\t\t" + path + "\n";
                        }
                    }
                }
                txt += "**********************\n";
                return txt;
            }
            public override string ToString()
            {
                string txt = "**********************\n";
                txt += "Manager " + Manager.Name + "\n";
                txt += "Used for types :\n";
                foreach (DataInformation data in dataInformations.Values)
                {
                    txt += "\t" + data.Name + "\n";
                }


                txt += "Has dependencyTypes with :\n";
                foreach (KeyValuePair<Type, List<string>> pair in dependencies)
                {
                    txt += "\t" + pair.Key.Name + " (" + pair.Key.Assembly.GetName().Name + ")\n";
                    foreach (string path in pair.Value)
                    {
                        txt += "\t\t" + path + "\n";
                    }
                }
                txt += "**********************\n";
                return txt;
            }
        }

    }

    public class PyramidInfo
    {
        public PyramidInfo? parent;
        public List<PyramidInfo> children = new();

        public Type type;
        public Type? aliasType;
        public List<DataMemberInfo> memberInfo = new();

        public bool isForceInherit = false;
        public bool nonGenericExtension = false;

        public Dictionary<Type, List<string>> dependencies = new();

        public PyramidInfo(Type type, List<DataMemberInfo> memberInfo)
        {
            this.type = type;
            this.memberInfo = memberInfo;
        }
    }

}
