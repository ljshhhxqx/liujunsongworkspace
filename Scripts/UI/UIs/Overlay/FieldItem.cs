using TMPro;
using UnityEngine;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class FieldItem : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI fieldName;
        [SerializeField]
        private TextMeshProUGUI fieldValue;


        public void SetField<T>(string fName, T value)
        {
            fieldName.text = fName;
            fieldValue.text = value.ToString();
        }
    }
}
