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
        [SerializeField] private string selectedTypeName; 
        [SerializeField] private TextAsset jsonTemplate;  
        [SerializeField] private bool useCamelCase = true;
        [SerializeField] private bool enumAsString = true;

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
            //var deserializedObject = JsonConvert.DeserializeObject(json, dataInstance.GetType());
            GUIUtility.systemCopyBuffer = json;
            Debug.Log($"JSON generated and copied to clipboard:\n{json} \n{GUIUtility.systemCopyBuffer}");
            
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
                    object fieldValue = field.GetValue(instance);
                    string fieldLabel = string.IsNullOrEmpty(prefix) ? field.Name : $"{prefix}.{field.Name}";

                    if (field.FieldType.IsPrimitive || field.FieldType == typeof(string) || field.FieldType.IsEnum)
                    {
                        object newValue = DrawField(fieldLabel, field.FieldType, fieldValue);
                        if (newValue != null && !Equals(newValue, fieldValue))
                        {
                            field.SetValue(instance, newValue);
                        }
                    }
                    else if (field.FieldType.IsClass || (field.FieldType.IsValueType && !field.FieldType.IsPrimitive))
                    {
                        // 处理嵌套对象
                        if (fieldValue == null && field.FieldType.IsClass)
                        {
                            fieldValue = Activator.CreateInstance(field.FieldType);
                            field.SetValue(instance, fieldValue);
                        }

                        EditorGUILayout.BeginVertical(GUI.skin.box);
                        EditorGUILayout.LabelField(field.Name, EditorStyles.boldLabel);
                        object processedValue = fieldValue;
                        if (fieldValue != null)
                        {
                            // 递归处理嵌套字段并获取处理后的实例
                            DrawNestedFields(fieldValue, field.FieldType, fieldLabel);
                            processedValue = fieldValue; // 对于引用类型，已直接修改；对于值类型，需要重新赋值
                        }
                        EditorGUILayout.EndVertical();

                        // 对于值类型，将处理后的实例设置回父字段
                        if (field.FieldType.IsValueType)
                        {
                            field.SetValue(instance, processedValue);
                        }
                    }
                }
            }

            private object DrawField(string label, Type fieldType, object value)
            {
                if (fieldType == typeof(string))
                    return EditorGUILayout.TextField(label, (string)value);
                if (fieldType == typeof(int))
                    return EditorGUILayout.IntField(label, (int)(value ?? 0));
                if (fieldType == typeof(float))
                    return EditorGUILayout.FloatField(label, (float)(value ?? 0f));
                if (fieldType.IsEnum)
                    return EditorGUILayout.EnumPopup(label, (Enum)(value ?? Enum.GetValues(fieldType).GetValue(0)));
                
                EditorGUILayout.LabelField($"{label}: Unsupported type {fieldType.Name}");
                return value;
            }
        }
    #endif
    }

}