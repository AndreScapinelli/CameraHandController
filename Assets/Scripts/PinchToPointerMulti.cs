using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;       // Para GraphicRaycaster
using System.Collections.Generic;
using LightBuzz.HandTracking;

public class PinchToPointerMultiTarget : MonoBehaviour
{
    [Header("Gerenciador que contém todos os HandVisual")]
    [SerializeField] private HandManager _handManager;

    [Header("Canvas em Screen Space - Camera")]
    [SerializeField] private Canvas _canvas;

    [Header("Câmera usada para Screen Space - Camera")]
    [SerializeField] private Camera _uiCamera;

    // Precisamos de um GraphicRaycaster para descobrir qual objeto foi “clicado”
    private GraphicRaycaster _raycaster;

    // Dicionário: cada mão (HandVisual) tem seu PointerEventData
    private Dictionary<HandVisual, PointerEventData> _pointerDataMap
        = new Dictionary<HandVisual, PointerEventData>();

    // Dicionário: qual objeto cada mão está arrastando
    private Dictionary<HandVisual, GameObject> _draggedObjectMap
        = new Dictionary<HandVisual, GameObject>();

    // Dicionário: estado de pinch no frame anterior
    private Dictionary<HandVisual, bool> _wasPinchingMap
        = new Dictionary<HandVisual, bool>();

    // Dicionário: última posição do pinch (para delta no Drag)
    private Dictionary<HandVisual, Vector2> _lastPinchPos
        = new Dictionary<HandVisual, Vector2>();

    private void Awake()
    {
        if (EventSystem.current == null)
        {
            Debug.LogError("Nenhum EventSystem encontrado na cena. Crie um GameObject -> UI -> Event System!");
        }

        // Certificar que o Canvas está configurado como Screen Space - Camera
        if (_canvas != null && _uiCamera != null)
        {
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = _uiCamera;
        }

        // Pegar ou criar um GraphicRaycaster no canvas
        if (_canvas != null)
        {
            _raycaster = _canvas.GetComponent<GraphicRaycaster>();
            if (_raycaster == null)
            {
                _raycaster = _canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }
    }

    private void Update()
    {
        if (_handManager == null || _raycaster == null) return;

        List<HandVisual> visuals = _handManager.HandVisuals;
        for (int i = 0; i < visuals.Count; i++)
        {
            HandVisual hv = visuals[i];
            if (hv == null) continue;

            bool isPinchingNow = hv.IsPinching;

            // 1) Obtemos a posição do pinch em coords de TELA
            // Se “vem do centro da tela”, precisamos converter
            // Mas assumindo aqui que "GetPinchCenter()" já retorna algo relativo à tela
            // Se estiver negativo ou fora, você deve corrigir dentro do HandVisual.
            Vector2 pinchPos = hv.GetPinchCenter();

            // Assegura que temos estado no dictionary
            if (!_pointerDataMap.ContainsKey(hv))
                _pointerDataMap[hv] = CreatePointerEventData(1000 + i);

            if (!_wasPinchingMap.ContainsKey(hv))
                _wasPinchingMap[hv] = false;

            if (!_draggedObjectMap.ContainsKey(hv))
                _draggedObjectMap[hv] = null;

            if (!_lastPinchPos.ContainsKey(hv))
                _lastPinchPos[hv] = pinchPos;

            bool wasPinching = _wasPinchingMap[hv];

            // Transições
            if (isPinchingNow && !wasPinching)
            {
                // Começou pinch => RaycastUI e PointerDown
                GameObject hitObj = RaycastUI(pinchPos);
                if (hitObj != null)
                {
                    _draggedObjectMap[hv] = hitObj;
                    FirePointerDown(hv, pinchPos, hitObj);
                }
            }
            else if (isPinchingNow && wasPinching)
            {
                // Continua pinch => Drag
                GameObject draggingObj = _draggedObjectMap[hv];
                if (draggingObj != null)
                {
                    FirePointerDrag(hv, pinchPos, draggingObj);
                }
            }
            else if (!isPinchingNow && wasPinching)
            {
                // Terminou pinch => PointerUp
                GameObject draggingObj = _draggedObjectMap[hv];
                if (draggingObj != null)
                {
                    FirePointerUp(hv, pinchPos, draggingObj);
                    _draggedObjectMap[hv] = null;
                }
            }

            _wasPinchingMap[hv] = isPinchingNow;
            _lastPinchPos[hv] = pinchPos;
        }
    }

    private PointerEventData CreatePointerEventData(int pointerId)
    {
        PointerEventData ped = new PointerEventData(EventSystem.current);
        ped.pointerId = pointerId;
        return ped;
    }

    // Quando pinch começa => PointerDown + initializePotentialDrag
    private void FirePointerDown(HandVisual hv, Vector2 pinchPos, GameObject targetObj)
    {
        PointerEventData ped = _pointerDataMap[hv];
        ped.position = pinchPos;
        ped.delta = Vector2.zero;
        ped.dragging = false;

        ExecuteEvents.Execute(targetObj, ped, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(targetObj, ped, ExecuteEvents.initializePotentialDrag);

        ped.dragging = true;
    }

    // Quando pinch continua => Drag
    private void FirePointerDrag(HandVisual hv, Vector2 pinchPos, GameObject targetObj)
    {
        PointerEventData ped = _pointerDataMap[hv];

        Vector2 lastPos = _lastPinchPos[hv];
        Vector2 delta = pinchPos - lastPos;

        ped.position = pinchPos;
        ped.delta = delta;
        ped.dragging = true;

        ExecuteEvents.Execute(targetObj, ped, ExecuteEvents.dragHandler);
    }

    // Quando pinch solta => PointerUp + endDrag
    private void FirePointerUp(HandVisual hv, Vector2 pinchPos, GameObject targetObj)
    {
        PointerEventData ped = _pointerDataMap[hv];
        ped.position = pinchPos;
        ped.delta = Vector2.zero;
        ped.dragging = false;

        ExecuteEvents.Execute(targetObj, ped, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(targetObj, ped, ExecuteEvents.endDragHandler);
    }

    // Faz o raycast na UI, pegando o objeto topmost
    private GameObject RaycastUI(Vector2 screenPos)
    {
        PointerEventData ped = new PointerEventData(EventSystem.current);
        ped.position = screenPos;

        List<RaycastResult> results = new List<RaycastResult>();
        _raycaster.Raycast(ped, results);
        if (results.Count > 0)
        {
            return results[0].gameObject; // topo
        }
        return null;
    }
}
