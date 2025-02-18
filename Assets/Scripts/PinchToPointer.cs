using LightBuzz.HandTracking;
using UnityEngine;
using UnityEngine.EventSystems;

public class PinchToPointer : MonoBehaviour
{
    [Header("Referência ao script que detecta pinch e retorna a posição de tela")]
    [SerializeField] private HandVisual _handVisual;

    [Header("Objeto de UI que receberá os eventos (precisa de IDragHandler, etc.)")]
    [SerializeField] private GameObject _draggableUI;

    // Armazena se está em pinch no frame anterior
    private bool _wasPinchingLastFrame = false;

    // Usaremos esse ID para nosso pointer "fake"
    private int _pointerId = 999;

    // Armazenamos as informações do pointer
    private PointerEventData _pointerData;

    private Vector2 _lastPinchPos;

    private void Awake()
    {
        // Cria uma vez o pointerData, associando ao EventSystem atual
        _pointerData = new PointerEventData(EventSystem.current)
        {
            pointerId = _pointerId
        };
    }

    private void Update()
    {
        if (_handVisual == null || _draggableUI == null) return;

        bool isPinchingNow = _handVisual.IsPinching;
        Vector2 pinchPos = _handVisual.GetPinchCenter();
        // 'GetPinchCenter' seria algo que retorna (thumbTip + indexTip)/2 em coordenadas de tela

        if (isPinchingNow && !_wasPinchingLastFrame)
        {
            // Iniciou pinch agora => PointerDown
            FirePointerDown(pinchPos);
        }
        else if (isPinchingNow && _wasPinchingLastFrame)
        {
            // Continua pinch => Drag
            FirePointerDrag(pinchPos);
        }
        else if (!isPinchingNow && _wasPinchingLastFrame)
        {
            // Pinch terminou => PointerUp
            FirePointerUp(pinchPos);
        }

        _wasPinchingLastFrame = isPinchingNow;
        _lastPinchPos = pinchPos;
    }

    private void FirePointerDown(Vector2 pinchPos)
    {
        _pointerData.pointerId = _pointerId;
        _pointerData.position = pinchPos;
        _pointerData.delta = Vector2.zero;
        _pointerData.dragging = false;
        _pointerData.pointerPressRaycast = new RaycastResult(); // se quiser, pode preencher
        _pointerData.pointerCurrentRaycast = new RaycastResult();

        // Dispara pointerDown
        ExecuteEvents.Execute(_draggableUI, _pointerData, ExecuteEvents.pointerDownHandler);

        // Sinaliza que agora está arrastando
        _pointerData.dragging = true;

        // Também podemos chamar 'InitializePotentialDragHandler' se o objeto precisar
        ExecuteEvents.Execute(_draggableUI, _pointerData, ExecuteEvents.initializePotentialDrag);
    }

    private void FirePointerDrag(Vector2 pinchPos)
    {
        Vector2 delta = pinchPos - _lastPinchPos;

        _pointerData.pointerId = _pointerId;
        _pointerData.position = pinchPos;
        _pointerData.delta = delta;
        _pointerData.dragging = true;

        // Dispara Drag
        ExecuteEvents.Execute(_draggableUI, _pointerData, ExecuteEvents.dragHandler);
    }

    private void FirePointerUp(Vector2 pinchPos)
    {
        _pointerData.pointerId = _pointerId;
        _pointerData.position = pinchPos;
        _pointerData.delta = Vector2.zero;
        _pointerData.dragging = false;

        // Dispara PointerUp
        ExecuteEvents.Execute(_draggableUI, _pointerData, ExecuteEvents.pointerUpHandler);

        // Opcional: 'EndDragHandler'
        ExecuteEvents.Execute(_draggableUI, _pointerData, ExecuteEvents.endDragHandler);
    }
}
