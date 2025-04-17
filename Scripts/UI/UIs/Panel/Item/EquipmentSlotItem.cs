using System;
using UniRx;
using UnityEngine.EventSystems;

namespace HotUpdate.Scripts.UI.UIs.Panel.Item
{
    public class EquipmentSlotItem : ItemBase, IPointerClickHandler
    {
        private readonly Subject<PointerEventData> _pointerClickObservable = new Subject<PointerEventData>();
        public IObservable<PointerEventData> OnPointerClickObservable => _pointerClickObservable;
        
        public override void SetData<T>(T data)
        {
            
        }

        public bool IsEmpty()
        {
            return false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _pointerClickObservable.OnNext(eventData);
        }
    }
}