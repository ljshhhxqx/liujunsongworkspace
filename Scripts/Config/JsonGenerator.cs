using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;
using AOTScripts.CustomAttribute;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace HotUpdate.Scripts.Config
{

    [CreateAssetMenu(fileName = "JsonGenerator", menuName = "Tools/Json Generator")]
    public class JsonGenerator : ScriptableObject
    {
        [SerializeField] private string selectedTypeName; // 存储选择的类型名
        [SerializeField] private TextAsset jsonTemplate;  // 可选：用于加载已有 JSON 数据
        [SerializeField] private bool useCamelCase = true;
        [SerializeField] private bool enumAsString = true;

        [SerializeField]
        private object dataInstance; // 核心数据实例
        private Type selectedType;

        public void GenerateJson()
        {
            if (dataInstance == null || selectedType == null)
            {
                Debug.LogError("No valid type selected or instance created!");
                return;
            }

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };

            if (useCamelCase)
            {
                settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
            }

            if (enumAsString)
            {
                settings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
            }

            string json = JsonConvert.SerializeObject(dataInstance, settings);
            GUIUtility.systemCopyBuffer = json;
            Debug.Log($"JSON generated and copied to clipboard:\n{json}");
        }

        public void SetSelectedType(Type type)
        {
            selectedType = type;
            selectedTypeName = type.Name;
            dataInstance = Activator.CreateInstance(type);
        }

        public object GetDataInstance()
        {
            return dataInstance;
        }

    #if UNITY_EDITOR
        [CustomEditor(typeof(JsonGenerator))]
        public class JsonGeneratorEditor : Editor
        {
            private List<Type> availableTypes = new List<Type>();
            private string[] typeNames;
            private int selectedIndex = 0;

            private void OnEnable()
            {
                availableTypes.Clear();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (Attribute.IsDefined(type, typeof(JsonSerializableAttribute)))
                        {
                            availableTypes.Add(type);
                        }
                    }
                }
                typeNames = new string[availableTypes.Count];
                for (int i = 0; i < availableTypes.Count; i++)
                {
                    typeNames[i] = availableTypes[i].Name;
                }

                JsonGenerator generator = (JsonGenerator)target;
                if (!string.IsNullOrEmpty(generator.selectedTypeName))
                {
                    selectedIndex = Array.IndexOf(typeNames, generator.selectedTypeName);
                    if (selectedIndex >= 0 && generator.GetDataInstance() == null)
                    {
                        generator.SetSelectedType(availableTypes[selectedIndex]);
                    }
                }
            }

            public override void OnInspectorGUI()
            {
                JsonGenerator generator = (JsonGenerator)target;

                EditorGUI.BeginChangeCheck();
                selectedIndex = EditorGUILayout.Popup("Select Type", selectedIndex, typeNames);
                if (EditorGUI.EndChangeCheck())
                {
                    generator.SetSelectedType(availableTypes[selectedIndex]);
                }

                EditorGUILayout.LabelField("Serialization Settings", EditorStyles.boldLabel);
                generator.useCamelCase = EditorGUILayout.Toggle("Use Camel Case", generator.useCamelCase);
                generator.enumAsString = EditorGUILayout.Toggle("Enum as String", generator.enumAsString);

                if (generator.GetDataInstance() != null)
                {
                    EditorGUILayout.LabelField("Configure Data", EditorStyles.boldLabel);
                    DrawNestedFields(generator.GetDataInstance(), generator.GetDataInstance().GetType());
                }

                if (GUILayout.Button("Generate JSON"))
                {
                    generator.GenerateJson();
                }

                EditorGUILayout.ObjectField("JSON Template (Optional)", generator.jsonTemplate, typeof(TextAsset), false);

                // 标记为脏，确保修改被保存
                if (GUI.changed)
                {
                    EditorUtility.SetDirty(generator);
                }
            }

            private void DrawNestedFields(object instance, Type type, string prefix = "")
            {
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    object value = field.GetValue(instance);
                    string fieldLabel = string.IsNullOrEmpty(prefix) ? field.Name : $"{prefix}.{field.Name}";

                    if (field.FieldType.IsPrimitive || field.FieldType == typeof(string) || field.FieldType.IsEnum)
                    {
                        object newValue = DrawField(fieldLabel, field.FieldType, value);
                        if (newValue != null && !Equals(newValue, value))
                        {
                            field.SetValue(instance, newValue);
                        }
                    }
                    else if (field.FieldType.IsClass || (field.FieldType.IsValueType && !field.FieldType.IsPrimitive))
                    {
                        // 处理嵌套对象
                        if (value == null)
                        {
                            value = Activator.CreateInstance(field.FieldType);
                            field.SetValue(instance, value); // 初始化并保存到实例
                        }

                        EditorGUILayout.BeginVertical(GUI.skin.box);
                        EditorGUILayout.LabelField(field.Name, EditorStyles.boldLabel);
                        DrawNestedFields(value, field.FieldType, fieldLabel); // 递归编辑嵌套字段
                        EditorGUILayout.EndVertical();
                    }
                }
            }

            private object DrawField(string label, Type fieldType, object value)
            {
                if (fieldType == typeof(string))
                    return EditorGUILayout.TextField(label, (string)value);
                else if (fieldType == typeof(int))
                    return EditorGUILayout.IntField(label, (int)(value ?? 0));
                else if (fieldType == typeof(float))
                    return EditorGUILayout.FloatField(label, (float)(value ?? 0f));
                else if (fieldType.IsEnum)
                    return EditorGUILayout.EnumPopup(label, (Enum)(value ?? Enum.GetValues(fieldType).GetValue(0)));
                else
                {
                    EditorGUILayout.LabelField($"{label}: Unsupported type {fieldType.Name}");
                    return null;
                }
            }
        }
    #endif
    }

}