using System;
using UnityEngine.EventSystems;

namespace GameLogic.Takeover.UI
{
    public sealed class TakeoverSquadBadgePointer : UnityEngine.MonoBehaviour, IPointerDownHandler
    {
        private Action _pointerDown;

        public void Bind(Action pointerDown)
        {
            _pointerDown = pointerDown;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                _pointerDown?.Invoke();
        }
    }
}
