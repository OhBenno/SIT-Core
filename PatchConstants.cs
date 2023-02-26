using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Communications;
using FilesChecker;
using HarmonyLib;
using Newtonsoft.Json;
using SIT.Tarkov.Core.AI;
using SIT.Tarkov.Core.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static SIT.Core.Misc.PaulovJsonConverters;

namespace SIT.Tarkov.Core
{
    public static class PatchConstants
    {
        public static BindingFlags PrivateFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static Type[] _eftTypes;
        public static Type[] EftTypes
        {
            get
            {
                if (_eftTypes == null)
                {
                    _eftTypes = typeof(AbstractGame).Assembly.GetTypes().OrderBy(t => t.Name).ToArray();
                }

                return _eftTypes;
            }
        }
        public static Type[] FilesCheckerTypes { get; private set; }
        public static Type LocalGameType { get; private set; }
        public static Type ExfilPointManagerType { get; private set; }
        public static Type BackendInterfaceType { get; private set; }
        public static Type SessionInterfaceType { get; private set; }

        public static Type StartWithTokenType { get; private set; }

        public static Type PoolManagerType { get; set; }

        public static Type JobPriorityType { get; set; }

        public static Type PlayerInfoType { get; set; }
        public static Type PlayerCustomizationType { get; set; }

        public static Type SpawnPointSystemInterfaceType { get; set; }
        public static Type SpawnPointArrayInterfaceType { get; set; }
        public static Type SpawnPointSystemClassType { get; set; }

        public static Type BackendStaticConfigurationType { get; set; }
        public static object BackendStaticConfigurationConfigInstance { get; set; }

        public static class CharacterControllerSettings
        {
            public static object CharacterControllerInstance { get; set; }
            public static CharacterControllerSpawner.Mode ObservedPlayerMode { get; set; }
            public static CharacterControllerSpawner.Mode ClientPlayerMode { get; set; }
            public static CharacterControllerSpawner.Mode BotPlayerMode { get; set; }
        }


        /// <summary>
        /// A Key/Value dictionary of storing & obtaining an array of types by name
        /// </summary>
        public static readonly Dictionary<string, Type[]> TypesDictionary = new();

        /// <summary>
        /// A Key/Value dictionary of storing & obtaining a type by name
        /// </summary>
        public static Dictionary<string, Type> TypeDictionary { get; } = new();

        /// <summary>
        /// A Key/Value dictionary of storing & obtaining a method by type and name
        /// </summary>
        public static readonly Dictionary<(Type, string), MethodInfo> MethodDictionary = new();

        private static string backendUrl;
        /// <summary>
        /// Method that returns the Backend Url (Example: https://127.0.0.1)
        /// </summary>
        public static string GetBackendUrl()
        {
            if (string.IsNullOrEmpty(backendUrl))
            {
                backendUrl = BackendConnection.GetBackendConnection().BackendUrl;
            }
            return backendUrl;
        }

        public static string GetPHPSESSID()
        {
            if (BackendConnection.GetBackendConnection() == null)
                Logger.LogError("Cannot get Backend Info");

            return BackendConnection.GetBackendConnection().PHPSESSID;
        }

        public static void DisplayMessageNotification(string message)
        {
            if (MessageNotificationType == null)
            {
                Logger.LogError("MessageNotificationType not found");
                return;
            }


            var o = MessageNotificationType.GetMethod("DisplayMessageNotification", BindingFlags.Static | BindingFlags.Public);
            if (o != null)
            {
                o.Invoke("DisplayMessageNotification", new object[] { message, ENotificationDurationType.Default, ENotificationIconType.Default, null });
            }

        }

        public static ManualLogSource Logger { get; private set; }

        public static Type MessageNotificationType { get; private set; }
        public static Type GroupingType { get; }
        public static Type JsonConverterType { get; }
        public static JsonConverter[] JsonConverterDefault { get; }

        private static ISession _backEndSession;
        public static ISession BackEndSession
        {
            get
            {
                if (_backEndSession == null)
                {
                    _backEndSession = Singleton<TarkovApplication>.Instance.GetClientBackEndSession();
                }

                return _backEndSession;
            }
        }

        public static T DoSafeConversion<T>(object o)
        {
            var json = o.SITToJson();
            return json.SITParseJson<T>();
        }

        public static object GetSingletonInstance(Type singletonInstanceType)
        {
            Type generic = typeof(Singleton<>);
            Type[] typeArgs = { singletonInstanceType };
            var genericType = generic.MakeGenericType(typeArgs);
            return GetPropertyFromType(genericType, "Instance").GetValue(null, null);
        }

        public static PropertyInfo GetPropertyFromType(Type t, string name)
        {
            var properties = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            PropertyInfo property = properties.FirstOrDefault(x => x.Name.ToLower() == name.ToLower());
            if (property != null)
                return property;

            return null;
        }

        public static FieldInfo GetFieldFromType(Type t, string name)
        {
            var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            return fields.FirstOrDefault(x => x.Name.ToLower() == name.ToLower());

        }

        public static FieldInfo GetFieldFromTypeByFieldType(Type objectType, Type fieldType)
        {
            var fields = objectType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            return fields.FirstOrDefault(x => x.FieldType == fieldType);

        }

        public static PropertyInfo GetPropertyFromTypeByPropertyType(Type objectType, Type propertyType)
        {
            var fields = objectType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            return fields.FirstOrDefault(x => x.PropertyType == propertyType);

        }

        public static MethodInfo GetMethodForType(Type t, string methodName, bool debug = false)
        {
            if (t == null)
            {
                Logger.LogError("GetMethodForType. t is NULL");
                return null;
            }
            return GetAllMethodsForType(t, debug).LastOrDefault(x => x.Name.ToLower() == methodName.ToLower());
        }

        public static async Task<MethodInfo> GetMethodForTypeAsync(Type t, string methodName, bool debug = false)
        {
            return await Task.Run(() => GetMethodForType(t, methodName, debug));
        }


        public static IEnumerable<MethodInfo> GetAllMethodsForType(Type t, bool debug = false)
        {
            foreach (var m in t.GetMethods(
                BindingFlags.NonPublic
                | BindingFlags.Public
                | BindingFlags.Static
                | BindingFlags.Instance
                | BindingFlags.FlattenHierarchy
                | BindingFlags.CreateInstance
                ))
            {
                if (debug)
                    Logger.LogInfo(m.Name);

                yield return m;
            }

            if (t.BaseType != null)
            {
                foreach (var m in t.BaseType.GetMethods(
                BindingFlags.NonPublic
                | BindingFlags.Public
                | BindingFlags.Static
                | BindingFlags.Instance
                | BindingFlags.FlattenHierarchy
                ))
                {
                    if (debug)
                        Logger.LogInfo(m.Name);

                    yield return m;
                }
            }

        }

        public static IEnumerable<MethodInfo> GetAllMethodsForObject(object ob)
        {
            return GetAllMethodsForType(ob.GetType());
        }

        public static IEnumerable<PropertyInfo> GetAllPropertiesForObject(object o)
        {
            if (o == null)
                return new List<PropertyInfo>();

            var t = o.GetType();
            var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public).ToList();
            props.AddRange(t.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic));
            props.AddRange(t.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy));
            props.AddRange(t.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy));
            props.AddRange(t.GetProperties(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy));
            if (t.BaseType != null)
            {
                t = t.BaseType;
                props.AddRange(t.GetProperties(BindingFlags.Instance | BindingFlags.Public));
                props.AddRange(t.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic));
                props.AddRange(t.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy));
                props.AddRange(t.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy));
                props.AddRange(t.GetProperties(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy));
            }
            return props.Distinct(x => x.Name).AsEnumerable();
        }

        public static IEnumerable<FieldInfo> GetAllFieldsForObject(object o)
        {
            var t = o.GetType();
            var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public).ToList();
            fields.AddRange(t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic));
            fields.AddRange(t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy));
            fields.AddRange(t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy));
            fields.AddRange(t.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy));
            if (t.BaseType != null)
            {
                t = t.BaseType;
                fields.AddRange(t.GetFields(BindingFlags.Instance | BindingFlags.Public));
                fields.AddRange(t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic));
                fields.AddRange(t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy));
                fields.AddRange(t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy));
                fields.AddRange(t.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy));
            }
            return fields.Distinct(x => x.Name).AsEnumerable();
        }

        public static T GetFieldOrPropertyFromInstance<T>(object o, string name, bool safeConvert = true)
        {
            PropertyInfo property = GetAllPropertiesForObject(o).FirstOrDefault(x => x.Name.ToLower() == name.ToLower());
            if (property != null)
            {
                if (safeConvert)
                    return DoSafeConversion<T>(property.GetValue(o));
                else
                    return (T)property.GetValue(o);
            }
            FieldInfo field = GetAllFieldsForObject(o).FirstOrDefault(x => x.Name.ToLower() == name.ToLower());
            if (field != null)
            {
                if (safeConvert)
                    return DoSafeConversion<T>(field.GetValue(o));
                else
                    return (T)field.GetValue(o);
            }

            return default(T);
        }

        public static async Task<T> GetFieldOrPropertyFromInstanceAsync<T>(object o, string name, bool safeConvert = true)
        {
            return await Task.Run(() => GetFieldOrPropertyFromInstance<T>(o, name, safeConvert));
        }

        public static void SetFieldOrPropertyFromInstance(object o, string name, object v)
        {
            var field = GetAllFieldsForObject(o).FirstOrDefault(x => x.Name.ToLower() == (name.ToLower()));
            if (field != null)
                field.SetValue(o, v);

            var property = GetAllPropertiesForObject(o).FirstOrDefault(x => x.Name.ToLower() == (name.ToLower()));
            if (property != null)
                property.SetValue(o, v);
        }

        public static void SetFieldOrPropertyFromInstance<T>(object o, string name, T v)
        {
            var field = GetAllFieldsForObject(o).FirstOrDefault(x => x.Name.ToLower() == (name.ToLower()));
            if (field != null)
                field.SetValue(o, v);

            var property = GetAllPropertiesForObject(o).FirstOrDefault(x => x.Name.ToLower() == (name.ToLower()));
            if (property != null)
                property.SetValue(o, v);
        }

        public static void ConvertDictionaryToObject(object o, Dictionary<string, object> dict)
        {
            foreach (var key in dict)
            {
                var prop = GetPropertyFromType(o.GetType(), key.Key);
                if (prop != null)
                {
                    prop.SetValue(o, key.Value);
                }
                var field = GetFieldFromType(o.GetType(), key.Key);
                if (field != null)
                {
                    field.SetValue(o, key.Value);
                }
            }
        }

        public static JsonConverter[] GetJsonConvertersBSG()
        {
            return JsonConverterDefault;
        }

        public static List<JsonConverter> GetJsonConvertersPaulov()
        {
            var converters = new List<JsonConverter>();
            converters.Add(new DateTimeOffsetJsonConverter());
            converters.Add(new SimpleCharacterControllerJsonConverter());
            converters.Add(new CollisionFlagsJsonConverter());
            converters.Add(new PlayerJsonConverter());
            return converters;
        }

        public static JsonSerializerSettings GetJsonSerializerSettings()
        {
            var converters = JsonConverterDefault;
            converters.AddItem(new DateTimeOffsetJsonConverter());
            converters.AddItem(new SimpleCharacterControllerJsonConverter());
            converters.AddItem(new CollisionFlagsJsonConverter());
            //converters.AddItem(new PlayerJsonConverter());
            var paulovconverters = GetJsonConvertersPaulov();
            converters.AddRangeToArray(paulovconverters.ToArray());

            return new JsonSerializerSettings()
            {
                Converters = converters,
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                Error = (serializer, err) =>
                {
                    Logger.LogError(err.ErrorContext.Error.ToString());
                }
            };
        }
        public static JsonSerializerSettings GetJsonSerializerSettingsWithoutBSG()
        {
            var converters = GetJsonConvertersPaulov();

            return new JsonSerializerSettings()
            {
                Converters = converters,
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                Error = (serializer, err) =>
                {
                    Logger.LogError(err.ErrorContext.Error.ToString());
                }
            };
        }

        public static string SITToJson(this object o)
        {


            return JsonConvert.SerializeObject(o
                    , GetJsonSerializerSettings()
                );
        }

        public static async Task<string> SITToJsonAsync(this object o)
        {
            return await Task.Run(() =>
            {
                return SITToJson(o);
            });
        }

        public static T SITParseJson<T>(this string str)
        {
            return (T)JsonConvert.DeserializeObject<T>(str
                    , new JsonSerializerSettings()
                    {
                        Converters = JsonConverterDefault
                        ,
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                    }
                    );
        }

        public static object GetPlayerProfile(object __instance)
        {
            var instanceProfile = __instance.GetType().GetProperty("Profile"
                , BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy).GetValue(__instance);
            if (instanceProfile == null)
            {
                Logger.LogInfo("ReplaceInPlayer:PatchPostfix: Couldn't find Profile");
                return null;
            }
            return instanceProfile;
        }

        public static string GetPlayerProfileAccountId(object instanceProfile)
        {
            var instanceAccountProp = instanceProfile.GetType().GetField("AccountId"
                , BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            if (instanceAccountProp == null)
            {
                Logger.LogInfo($"ReplaceInPlayer:PatchPostfix: instanceAccountProp not found");
                return null;
            }
            var instanceAccountId = instanceAccountProp.GetValue(instanceProfile).ToString();
            return instanceAccountId;
        }

        public static IDisposable StartWithToken(string name)
        {
            return GetAllMethodsForType(StartWithTokenType).Single(x => x.Name == "StartWithToken").Invoke(null, new object[] { name }) as IDisposable;
        }

        public static async Task InvokeAsyncStaticByReflection(MethodInfo methodInfo, object rModel, params object[] p)
        {
            if (rModel == null)
            {
                await (Task)methodInfo
                    .MakeGenericMethod(new[] { rModel.GetType() })
                    .Invoke(null, p);
            }
            else
            {
                await (Task)methodInfo
                    .Invoke(null, p);
            }
        }

        public static ClientApplication<ISession> GetClientApp()
        {
            return Singleton<ClientApplication<ISession>>.Instance;
        }

        public static TarkovApplication GetMainApp()
        {
            return GetClientApp() as TarkovApplication;
        }

        /// <summary>
        /// Invoke an async Task<object> method
        /// </summary>
        /// <param name="type"></param>
        /// <param name="outputType"></param>
        /// <param name="method"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        //public static async Task<object> InvokeAsyncMethod(Type type, Type outputType, string method, object[] param)
        //{
        //    var m = PatchConstants.GetAllMethodsForType(type).First(x => x.Name == method);// foo.GetType().GetMethod(nameof(IFoo.Get));
        //    Logger.LogInfo("InvokeAsyncMethod." + m.Name);

        //    //var builder = AsyncTaskMethodBuilder.Create();

        //    var generic = m.MakeGenericMethod(outputType);
        //    var task = (Task)generic.Invoke(type, param);

        //    await task.ConfigureAwait(false);

        //    var resultProperty = task.GetType().GetProperty("Result");
        //    return resultProperty.GetValue(task);

        //}

        static PatchConstants()
        {
            if (Logger == null)
                Logger = BepInEx.Logging.Logger.CreateLogSource("SIT.Tarkov.Core.PatchConstants");

            TypesDictionary.Add("EftTypes", EftTypes);

            FilesCheckerTypes = typeof(ICheckResult).Assembly.GetTypes();
            LocalGameType = EftTypes.Single(x => x.Name == "LocalGame");
            ExfilPointManagerType = EftTypes.Single(x => x.GetMethod("InitAllExfiltrationPoints") != null);
            BackendInterfaceType = EftTypes.Single(x => x.GetMethods().Select(y => y.Name).Contains("CreateClientSession") && x.IsInterface);
            SessionInterfaceType = EftTypes.Single(x => x.GetMethods().Select(y => y.Name).Contains("GetPhpSessionId") && x.IsInterface);
            MessageNotificationType = EftTypes.Single(x => x.GetMethods(BindingFlags.Static | BindingFlags.Public).Select(y => y.Name).Contains("DisplayMessageNotification"));
            if (MessageNotificationType == null)
            {
                Logger.LogInfo("SIT.Tarkov.Core:PatchConstants():MessageNotificationType:Not Found");
            }
            GroupingType = EftTypes.Single(x => x.GetMethods(BindingFlags.Public | BindingFlags.Static).Select(y => y.Name).Contains("CreateRaidPlayer"));
            //if (GroupingType != null)
            //{
            //  Logger.LogInfo("SIT.Tarkov.Core:PatchConstants():Found GroupingType:" + GroupingType.FullName);
            //}

            JsonConverterType = typeof(AbstractGame).Assembly.GetTypes()
               .First(t => t.GetField("Converters", BindingFlags.Static | BindingFlags.Public) != null);
            JsonConverterDefault = JsonConverterType.GetField("Converters", BindingFlags.Static | BindingFlags.Public).GetValue(null) as JsonConverter[];
            //Logger.LogInfo($"PatchConstants: {JsonConverterDefault.Length} JsonConverters found");

            StartWithTokenType = EftTypes.Single(x => GetAllMethodsForType(x).Count(y => y.Name == "StartWithToken") == 1);

            BotSystemHelpers.Setup();

            if (JobPriorityType == null)
            {
                JobPriorityType = EftTypes.Single(x =>
                    GetAllMethodsForType(x).Any(x => x.Name == "Priority")
                    );
                //Logger.LogInfo($"Loading JobPriorityType:{JobPriorityType.FullName}");
            }

            if (PlayerInfoType == null)
            {
                PlayerInfoType = EftTypes.Single(x =>
                    GetAllMethodsForType(x).Any(x => x.Name == "AddBan")
                    && GetAllMethodsForType(x).Any(x => x.Name == "RemoveBan")
                    );
                //Logger.LogInfo($"Loading PlayerInfoType:{PlayerInfoType.FullName}");
            }

            if (PlayerCustomizationType == null)
            {
                PlayerCustomizationType = GetFieldFromType(typeof(Profile), "Customization").FieldType;
                //Logger.LogInfo($"Loading PlayerCustomizationType:{PlayerCustomizationType.FullName}");
            }

            SpawnPointArrayInterfaceType = EftTypes.Single(x =>
                        GetAllMethodsForType(x).Any(x => x.Name == "CreateSpawnPoint")
                        && GetAllMethodsForType(x).Any(x => x.Name == "DestroySpawnPoint")
                        && x.IsInterface
                    );
            //Logger.LogInfo($"Loading SpawnPointArrayInterfaceType:{SpawnPointArrayInterfaceType.FullName}");

            BackendStaticConfigurationType = EftTypes.Single(x =>
                    GetAllMethodsForType(x).Any(x => x.Name == "LoadApplicationConfig")
            //&& PatchConstants.GetFieldFromType(x, "BackendUrl") != null
            //&& PatchConstants.GetFieldFromType(x, "Config") != null
            );

            //Logger.LogInfo($"Loading BackendStaticConfigurationType:{BackendStaticConfigurationType.FullName}");

            if (!TypeDictionary.ContainsKey("StatisticsSession"))
            {
                TypeDictionary.Add("StatisticsSession", EftTypes.OrderBy(x => x.Name).First(x =>
                    x.IsClass
                    && GetAllMethodsForType(x).Any(x => x.Name == "BeginStatisticsSession")
                    && GetAllMethodsForType(x).Any(x => x.Name == "EndStatisticsSession")
                ));
                //Logger.LogInfo($"StatisticsSession:{TypeDictionary["StatisticsSession"].FullName}");
            }

            if (!TypeDictionary.ContainsKey("FilterCustomization"))
            {
                // Gather FilterCustomization
                TypeDictionary.Add("FilterCustomization", EftTypes.OrderBy(x => x.Name).Last(x =>
                    x.IsClass
                    && GetAllMethodsForType(x).Any(x => x.Name == "FilterCustomization")
                ));
                Logger.LogInfo($"FilterCustomization:{TypeDictionary["FilterCustomization"].FullName}");
            }

            TypeDictionary.Add("Profile", EftTypes.First(x =>
               x.IsClass && x.FullName == "EFT.Profile"
           ));

            TypeDictionary.Add("Profile.Customization", EftTypes.First(x =>
                x.IsClass
                && x.BaseType == typeof(Dictionary<EBodyModelPart, string>)
            ));

            TypeDictionary.Add("Profile.Inventory", EftTypes.First(x =>
                x.IsClass
                && GetAllMethodsForType(x).Any(x => x.Name == "UpdateTotalWeight")
                && GetAllMethodsForType(x).Any(x => x.Name == "GetAllItemByTemplate")
                && GetAllMethodsForType(x).Any(x => x.Name == "GetItemsInSlots")
            ));
        }
    }
}
