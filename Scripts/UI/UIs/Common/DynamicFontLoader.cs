using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(TextMeshProUGUI))]
public class DynamicFontLoader : MonoBehaviour
{
    public TMP_FontAsset baseFont;
    [SerializeField] 
    private bool autoUpdateInEditor = true;
    [SerializeField] 
    private bool isDynamicText = false;
    private TextMeshProUGUI _textComponent;
    private TextMeshProUGUI TextComponent 
    {
        get
        {
            if (!_textComponent)
            {
                _textComponent = GetComponent<TextMeshProUGUI>();
                if (!_textComponent)
                {
                    Debug.LogError($"TextMeshProUGUI component not found on this GameObject {name}.");
                    return null;
                }
            }
            return _textComponent;
        }
    }
    private readonly HashSet<uint> _loadedCharacters = new HashSet<uint>();
    private HashSet<char> _characterSet;
    private string _lastProcessedText = "";
    [SerializeField]
    private float updateInterval = 0.1f;
    private float _lastUpdateTime;

    private void Awake()
    {
        if (baseFont != null)
        {
            TextComponent.font = baseFont;
        }
    }

    private void OnEnable()
    {
        if (!TMP_Settings.fallbackFontAssets.Contains(baseFont))
        {
            TMP_Settings.fallbackFontAssets.Add(baseFont);
        }
    }

    private void Update()
    {
        _lastUpdateTime+= Time.deltaTime;
        if (_lastUpdateTime < updateInterval)
        {
            return;
        }
        _lastUpdateTime = 0;
        if (isDynamicText && !string.IsNullOrEmpty(TextComponent.text) && TextComponent.text != _lastProcessedText )
        {
            PrepareTextForDisplay(TextComponent.text);
            _lastProcessedText = TextComponent.text;
        }
    }

    public void PrepareTextForDisplay(string text)
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            LoadCharacterSetFromFile();
        }
        #endif

        List<uint> charactersToAdd = new List<uint>();

        foreach (char c in text)
        {
            uint unicode = c;
            if (!_loadedCharacters.Contains(unicode) && ShouldLoadCharacter(c))
            {
                if (!baseFont.HasCharacter(c))
                {
                    charactersToAdd.Add(unicode);
                }
                _loadedCharacters.Add(unicode);
            }
        }

        if (charactersToAdd.Count > 0)
        {
            LoadCharacters(charactersToAdd.ToArray());
        }

        TextComponent.text = text;
        TextComponent.ForceMeshUpdate();
    }

    private bool ShouldLoadCharacter(char c)
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            return _characterSet.Contains(c);
        }
        #endif
        return false;
    }

    private void LoadCharacters(uint[] unicodes)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // 编辑器模式下的加载逻辑
            string path = AssetDatabase.GetAssetPath(baseFont);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            SerializedObject fontAsset = new SerializedObject(baseFont);
            fontAsset.Update();

            SerializedProperty atlasPopulationMode = fontAsset.FindProperty("m_AtlasPopulationMode");
            atlasPopulationMode.intValue = (int)AtlasPopulationMode.Dynamic;

            fontAsset.ApplyModifiedProperties();

            if (baseFont.TryAddCharacters(unicodes))
            {
                Debug.Log($"Successfully loaded {unicodes.Length} characters in editor mode.");
                EditorUtility.SetDirty(baseFont);
                AssetDatabase.SaveAssets();
            }
            else
            {
                Debug.LogWarning($"Failed to load {unicodes.Length} characters in editor mode.");
            }
        }
        else
        {
            // 运行时的加载逻辑
            if (baseFont.TryAddCharacters(unicodes))
            {
                Debug.Log($"Successfully loaded {unicodes.Length} characters at runtime.");
            }
            else
            {
                Debug.LogWarning($"Failed to load {unicodes.Length} characters at runtime.");
            }
        }
#else
        // 构建后的运行时加载逻辑
        if (baseFont.TryAddCharacters(unicodes))
        {
            Debug.Log($"Successfully loaded {unicodes.Length} characters at runtime.");
        }
        else
        {
            Debug.LogWarning($"Failed to load {unicodes.Length} characters at runtime.");
        }
#endif
    }

    #if UNITY_EDITOR
    private void LoadCharacterSetFromFile()
    {
        if (_characterSet == null)
        {
            string path = Path.Combine(Application.dataPath, "2D Casual UI/Font", "3500Charactor.txt");
            string content = File.ReadAllText(path);
            _characterSet = new HashSet<char>(content);
        }
    }

    public void UpdateText()
    {
        if (TextComponent)
        {
            PrepareTextForDisplay(TextComponent.text);
        }
    }

    [CustomEditor(typeof(DynamicFontLoader))]
    public class DynamicFontLoaderEditor : Editor
    {
        private DynamicFontLoader loader;
        private string previousText = "";

        private void OnEnable()
        {
            loader = (DynamicFontLoader)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (GUILayout.Button("Update Text"))
            {
                loader.UpdateText();
            }

            if (loader.autoUpdateInEditor && loader.TextComponent != null)
            {
                string currentText = loader.TextComponent.text;
                if (currentText != previousText)
                {
                    loader.PrepareTextForDisplay(currentText);
                    previousText = currentText;
                }
            }
        }
    }
    #endif
}
