using UnityEngine;
using UnityEngine.EventSystems;

// Um exemplo de script para mover um RectTransform via OnDrag
public class DragHandler : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"{name} OnPointerDown");
    }

    public void OnDrag(PointerEventData eventData)
    {
        Debug.Log($"{name} OnDrag delta={eventData.delta}");
        // Exemplo: mover anchoredPosition
        RectTransform rt = GetComponent<RectTransform>();
        rt.anchoredPosition += eventData.delta;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log($"{name} OnPointerUp");
    }
}
