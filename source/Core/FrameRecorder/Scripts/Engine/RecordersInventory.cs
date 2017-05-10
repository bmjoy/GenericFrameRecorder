using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine.Recorder.FrameRecorder.Utilities;

namespace UnityEngine.Recorder.FrameRecorder
{
    public class RecorderInfo
    {
        public Type recorderType;
        public Type settings;
        public string category;
        public string displayName;

        public static RecorderInfo Instantiate<TRecoder, TSettings>(string category, string displayName)
            where TRecoder : class
            where TSettings : class
        {
            return new RecorderInfo()
            {
                recorderType = typeof(TRecoder),
                settings = typeof(TSettings),
                category = category,
                displayName = displayName
            };
        }
    }

    // to be internal once inside unity code base
    public static class RecordersInventory
    {
        internal static SortedDictionary<string, RecorderInfo> recorders { get; private set; }

        static void Init()
        {
#if UNITY_EDITOR
            if (RecordersInventory.recorders != null)
                return;

            RecordersInventory.recorders = new SortedDictionary<string, RecorderInfo>();
            var recorders = ClassHelpers.FilterByAttribute<FrameRecorderClassAttribute>(false);
            foreach (var recorder in recorders)
                AddRecorder(recorder.Key);
#endif
        }

#if UNITY_EDITOR
        static SortedDictionary<string, List<RecorderInfo>> m_RecordersByCategory;

        public static SortedDictionary<string, List<RecorderInfo>> recordersByCategory
        {
            get
            {
                Init();
                return m_RecordersByCategory;
            }
        }

        static string[] m_AvailableCategories;
        public static string[] availableCategories
        {
            get
            {
                if (m_AvailableCategories == null)
                {
                    m_AvailableCategories = RecordersInventory.ListRecorders()
                        .GroupBy(x => x.category)
                        .Select(x => x.Key)
                        .OrderBy(x => x)
                        .ToArray();
                }
                return m_AvailableCategories;
            }
        }
#endif

        static bool AddRecorder(Type recorderType)
        {
            RecorderInfo recInfo = null;
            var method = recorderType.GetMethod("GetRecorderInfo", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (method != null)
            {
                try
                {
                    recInfo = method.Invoke(null, null) as RecorderInfo;

                    if (recInfo != null)
                    {
                        if (recorders == null)
                            recorders = new SortedDictionary<string, RecorderInfo>();
                        recorders.Add(recInfo.recorderType.FullName, recInfo);

#if UNITY_EDITOR
                        if (m_RecordersByCategory == null)
                            m_RecordersByCategory = new SortedDictionary<string, List<RecorderInfo>>();

                        if (!m_RecordersByCategory.ContainsKey(recInfo.category))
                            m_RecordersByCategory.Add(recInfo.category, new List<RecorderInfo>());

                        m_RecordersByCategory[recInfo.category].Add(recInfo);
#endif
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            else
            {
                Debug.LogError(String.Format("The recorder class '{0}' need to provide method: static RecorderInfo GetRecorderInfo(...)", recorderType.FullName));
            }

            return false;
        }

        public static RecorderInfo GetRecorderInfo<TRecorder>() where TRecorder : class
        {
            return GetRecorderInfo(typeof(TRecorder));
        }

        public static RecorderInfo GetRecorderInfo(Type recorderType)
        {
            Init();
            if (recorders.ContainsKey(recorderType.FullName))
                return recorders[recorderType.FullName];

#if UNITY_EDITOR
            return null;
#else
            if (AddRecorder(recorderType))
                return recorders[recorderType.FullName];
            else
                return null
#endif
        }

        public static IEnumerable<RecorderInfo> ListRecorders()
        {
            Init();

            foreach (var recorderInfo in recorders)
            {
                yield return recorderInfo.Value;
            }
        }

        public static Recorder GenerateNewRecorder(Type recorderType, FrameRecorderSettings settings)
        {
            Init();
            var factory = GetRecorderInfo(recorderType);
            if (factory != null)
            {
                var recorder = ScriptableObject.CreateInstance(recorderType) as Recorder;
                recorder.Reset();
                recorder.settings = settings;
                return recorder;
            }
            else
                throw new ArgumentException("No factory was registered for " + recorderType.Name);
        }

        public static FrameRecorderSettings GenerateNewSettingsAsset(UnityEngine.Object parentAsset, Type recorderType)
        {
            Init();
            var recorderinfo = GetRecorderInfo(recorderType);
            if (recorderinfo != null)
            {
                FrameRecorderSettings settings = null;
                settings = ScriptableObject.CreateInstance(recorderinfo.settings) as FrameRecorderSettings;
                settings.name = "Frame Recorder Settings";
                settings.recorderType = recorderType;
                settings.hideFlags = HideFlags.HideInHierarchy;

                AssetDatabase.AddObjectToAsset(settings, parentAsset);
                AssetDatabase.SaveAssets();
                return settings;
            }
            else
                throw new ArgumentException("No factory was registered for " + recorderType.Name);            
        }

    }
}
