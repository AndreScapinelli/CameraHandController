using UnityEngine;
using UnityEngine.EventSystems;

public class DragHandler : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log("PointerDown no objeto: " + name);
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Se for UI em Canvas Screen Space - Overlay, normalmente podemos
        // usar eventData.delta e mover o RectTransform.
        // Mas o próprio Unity faz isso se esse objeto tiver um componente "Layout Element", etc.

        Vector2 delta = eventData.delta;
        // Exemplo: mover o RectTransform do objeto manualmente:
        RectTransform rt = GetComponent<RectTransform>();
        rt.anchoredPosition += delta;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log("PointerUp no objeto: " + name);
    }
}
