using UnityEngine;

namespace LightBuzz.HandTracking
{
    public class HandVisual : MonoBehaviour
    {
        [Header("2D or 3D space")]
        [SerializeField] private bool _is2D = false;

        [ConditionalHide("_is2D", false)]
        [SerializeField] private ImageView _image;

        [Header("Colors")]
        [SerializeField] private Color32 _leftHandColor = new Color32(0, 153, 229, 200);
        [SerializeField] private Color32 _leftBoxColor = new Color32(0, 153, 229, 128);
        [SerializeField] private Color32 _rightHandColor = new Color32(0, 230, 110, 200);
        [SerializeField] private Color32 _rightBoxColor = new Color32(0, 230, 110, 128);

        [Header("Points")]
        [SerializeField] private GameObject[] _points;

        [Header("Lines")]
        [SerializeField] private LineRenderer _thumb;
        [SerializeField] private LineRenderer _index;
        [SerializeField] private LineRenderer _middle;
        [SerializeField] private LineRenderer _ring;
        [SerializeField] private LineRenderer _pinky;
        [SerializeField] private LineRenderer _mcpLine;
        [SerializeField] private LineRenderer _palmLine;

        [SerializeField] private LineRenderer _boundingBox;

        [Header("Pinch Settings")]
        [Tooltip("Distância abaixo da qual é considerado pinch (polegar + indicador)")]
        [SerializeField] private float _pinchThreshold = 0.02f;

        /// <summary>
        /// Linha dedicada para conectar sempre o ThumbTip ao IndexTip.
        /// (Arraste um LineRenderer no Inspector aqui, se desejar.)
        /// </summary>
        [Header("Always-visible line between Thumb & Index")]
        [SerializeField] private LineRenderer _pinchLine;

        /// <summary>
        /// Guarda se estamos em pinça.
        /// </summary>
        private bool _isPinching = false;

        /// <summary>
        /// Guarda a distância atual entre polegar e indicador.
        /// </summary>
        private float _pinchDistance = 0f;

        /// <summary>
        /// Guardamos a cor padrão da _pinchLine para restaurar quando não estiver em pinch.
        /// </summary>
        private Color32 _pinchLineDefaultColor;

        /// <summary>
        /// Para expor as posições de tela do polegar e indicador (caso seja 2D).
        /// </summary>
        private Vector2 _thumbTipScreenPos;
        private Vector2 _indexTipScreenPos;

        private Hand _hand;
        private Vector3 _offset = Vector3.zero;

        public Hand Hand
        {
            get => _hand;
            set => _hand = value;
        }

        public Vector3 Offset
        {
            get => _offset;
            set => _offset = value;
        }

        public HandSide Side { get; private set; }

        /// <summary>
        /// Expor se está pinçando externamente (por exemplo, para outro script).
        /// </summary>
        public bool IsPinching => _isPinching;

        /// <summary>
        /// Distância atual entre o ThumbTip e IndexTip.
        /// </summary>
        public float PinchDistance => _pinchDistance;

        /// <summary>
        /// Retorna o ponto médio (em tela) entre o polegar e o indicador, caso esteja em 2D.
        /// Se estiver em 3D, pode ser necessário converter via WorldToScreenPoint antes.
        /// </summary>
        public Vector2 GetPinchCenter()
        {
            // Exemplo: pinch no "centro" da tela => (0,0).
            // Se X<0, é esquerda do centro. Se Y<0, é abaixo do centro.
            // Precisamos mover => (Screen.width/2, Screen.height/2).

            Vector2 center = (_thumbTipScreenPos + _indexTipScreenPos) * 0.5f;

            // Se (0,0) é o centro, vamos somar offset:
             center.x += Screen.width / 2f;
             center.y += Screen.height / 2f;

            Debug.Log(center);

            // APLIQUE se de fato for esse o caso. Se já estiver “certo”, não aplique.

            return center;
        }

        public bool Is2D
        {
            get => _is2D;
            set
            {
                _is2D = value;
                Recreate();
            }
        }

        public ImageView Image
        {
            get => _image;
            set => _image = value;
        }

        /// <summary>
        /// Ajusta as cores padrão (de acordo com L/R) e a espessura das linhas.
        /// É chamado sempre que a mão é atualizada ou quando mudamos _is2D.
        /// </summary>
        private void Recreate()
        {
            // Escolhe cor da mão conforme se é esquerda ou direita
            Color32 handColor = Side == HandSide.Left ? _leftHandColor : _rightHandColor;
            Color32 boxColor = Side == HandSide.Left ? _leftBoxColor : _rightBoxColor;

            // Define cores do "esqueleto"
            _thumb.startColor = handColor; _thumb.endColor = handColor;
            _index.startColor = handColor; _index.endColor = handColor;
            _middle.startColor = handColor; _middle.endColor = handColor;
            _ring.startColor = handColor; _ring.endColor = handColor;
            _pinky.startColor = handColor; _pinky.endColor = handColor;
            _mcpLine.startColor = handColor; _mcpLine.endColor = handColor;
            _palmLine.startColor = handColor; _palmLine.endColor = handColor;

            // Bounding box
            _boundingBox.startColor = boxColor;
            _boundingBox.endColor = boxColor;

            // Se tivermos a pinchLine, definimos sua cor padrão = mesma cor da mão
            if (_pinchLine != null)
            {
                _pinchLine.startColor = handColor;
                _pinchLine.endColor = handColor;
                _pinchLineDefaultColor = handColor; // Armazena a cor
            }

            // Se for modo 3D, definimos a espessura
            if (!_is2D)
            {
                float lineWidth = 0.01f;

                // Ajusta cada line renderer
                _thumb.startWidth = lineWidth; _thumb.endWidth = lineWidth;
                _index.startWidth = lineWidth; _index.endWidth = lineWidth;
                _middle.startWidth = lineWidth; _middle.endWidth = lineWidth;
                _ring.startWidth = lineWidth; _ring.endWidth = lineWidth;
                _pinky.startWidth = lineWidth; _pinky.endWidth = lineWidth;
                _mcpLine.startWidth = lineWidth; _mcpLine.endWidth = lineWidth;
                _palmLine.startWidth = lineWidth; _palmLine.endWidth = lineWidth;

                _boundingBox.startWidth = lineWidth;
                _boundingBox.endWidth = lineWidth;

                if (_pinchLine != null)
                {
                    _pinchLine.startWidth = lineWidth;
                    _pinchLine.endWidth = lineWidth;
                }
            }
        }

        /// <summary>
        /// Método chamado pelo HandManager para carregar/atualizar a posição dos dedos e desenhar tudo.
        /// </summary>
        public void Load(Hand hand, Vector3 offset = default, bool isFrontFacing = false)
        {
            _hand = hand;
            _offset = offset;

            if (_hand == null || _hand.Confidence < 0.0f)
            {
                Toggle(false);
                return;
            }

            // Define se a mão é esquerda ou direita
            Side = _hand.Side;

            // Ativamos todos os line renderers e refazemos cores
            Toggle(true);
            Recreate();

            // Carrega todos os finger joints importantes
            var root = _hand.FingerJoints[FingerJointType.Root];
            var thumbCmc = _hand.FingerJoints[FingerJointType.ThumbCMC];
            var thumbMcp = _hand.FingerJoints[FingerJointType.ThumbMCP];
            var thumbIp = _hand.FingerJoints[FingerJointType.ThumbIP];
            var thumbTip = _hand.FingerJoints[FingerJointType.ThumbTip];

            var indexMcp = _hand.FingerJoints[FingerJointType.IndexMCP];
            var indexPip = _hand.FingerJoints[FingerJointType.IndexPIP];
            var indexDip = _hand.FingerJoints[FingerJointType.IndexDIP];
            var indexTip = _hand.FingerJoints[FingerJointType.IndexTip];

            var middleMcp = _hand.FingerJoints[FingerJointType.MiddleMCP];
            var middlePip = _hand.FingerJoints[FingerJointType.MiddlePIP];
            var middleDip = _hand.FingerJoints[FingerJointType.MiddleDIP];
            var middleTip = _hand.FingerJoints[FingerJointType.MiddleTip];

            var ringMcp = _hand.FingerJoints[FingerJointType.RingMCP];
            var ringPip = _hand.FingerJoints[FingerJointType.RingPIP];
            var ringDip = _hand.FingerJoints[FingerJointType.RingDIP];
            var ringTip = _hand.FingerJoints[FingerJointType.RingTip];

            var pinkyMcp = _hand.FingerJoints[FingerJointType.PinkyMCP];
            var pinkyPip = _hand.FingerJoints[FingerJointType.PinkyPIP];
            var pinkyDip = _hand.FingerJoints[FingerJointType.PinkyDIP];
            var pinkyTip = _hand.FingerJoints[FingerJointType.PinkyTip];

            var palm = _hand.FingerJoints[FingerJointType.Palm];

            // Converte as posições (2D ou 3D)
            Vector3 pRoot = _is2D ? GetPosition2D(root.Position2D) : GetPosition3D(root.Position3D, isFrontFacing);
            Vector3 pThumbCmc = _is2D ? GetPosition2D(thumbCmc.Position2D) : GetPosition3D(thumbCmc.Position3D, isFrontFacing);
            Vector3 pThumbMcp = _is2D ? GetPosition2D(thumbMcp.Position2D) : GetPosition3D(thumbMcp.Position3D, isFrontFacing);
            Vector3 pThumbIp = _is2D ? GetPosition2D(thumbIp.Position2D) : GetPosition3D(thumbIp.Position3D, isFrontFacing);
            Vector3 pThumbTip = _is2D ? GetPosition2D(thumbTip.Position2D) : GetPosition3D(thumbTip.Position3D, isFrontFacing);

            Vector3 pIndexMcp = _is2D ? GetPosition2D(indexMcp.Position2D) : GetPosition3D(indexMcp.Position3D, isFrontFacing);
            Vector3 pIndexPip = _is2D ? GetPosition2D(indexPip.Position2D) : GetPosition3D(indexPip.Position3D, isFrontFacing);
            Vector3 pIndexDip = _is2D ? GetPosition2D(indexDip.Position2D) : GetPosition3D(indexDip.Position3D, isFrontFacing);
            Vector3 pIndexTip = _is2D ? GetPosition2D(indexTip.Position2D) : GetPosition3D(indexTip.Position3D, isFrontFacing);

            Vector3 pMiddleMcp = _is2D ? GetPosition2D(middleMcp.Position2D) : GetPosition3D(middleMcp.Position3D, isFrontFacing);
            Vector3 pMiddlePip = _is2D ? GetPosition2D(middlePip.Position2D) : GetPosition3D(middlePip.Position3D, isFrontFacing);
            Vector3 pMiddleDip = _is2D ? GetPosition2D(middleDip.Position2D) : GetPosition3D(middleDip.Position3D, isFrontFacing);
            Vector3 pMiddleTip = _is2D ? GetPosition2D(middleTip.Position2D) : GetPosition3D(middleTip.Position3D, isFrontFacing);

            Vector3 pRingMcp = _is2D ? GetPosition2D(ringMcp.Position2D) : GetPosition3D(ringMcp.Position3D, isFrontFacing);
            Vector3 pRingPip = _is2D ? GetPosition2D(ringPip.Position2D) : GetPosition3D(ringPip.Position3D, isFrontFacing);
            Vector3 pRingDip = _is2D ? GetPosition2D(ringDip.Position2D) : GetPosition3D(ringDip.Position3D, isFrontFacing);
            Vector3 pRingTip = _is2D ? GetPosition2D(ringTip.Position2D) : GetPosition3D(ringTip.Position3D, isFrontFacing);

            Vector3 pPinkyMcp = _is2D ? GetPosition2D(pinkyMcp.Position2D) : GetPosition3D(pinkyMcp.Position3D, isFrontFacing);
            Vector3 pPinkyPip = _is2D ? GetPosition2D(pinkyPip.Position2D) : GetPosition3D(pinkyPip.Position3D, isFrontFacing);
            Vector3 pPinkyDip = _is2D ? GetPosition2D(pinkyDip.Position2D) : GetPosition3D(pinkyDip.Position3D, isFrontFacing);
            Vector3 pPinkyTip = _is2D ? GetPosition2D(pinkyTip.Position2D) : GetPosition3D(pinkyTip.Position3D, isFrontFacing);

            Vector3 pPalm = _is2D ? GetPosition2D(palm.Position2D) : GetPosition3D(palm.Position3D, isFrontFacing);

            // Posiciona cada ponto
            _points[0].transform.position = pRoot;
            _points[1].transform.position = pThumbCmc;
            _points[2].transform.position = pThumbMcp;
            _points[3].transform.position = pThumbIp;
            _points[4].transform.position = pThumbTip;
            _points[5].transform.position = pIndexMcp;
            _points[6].transform.position = pIndexPip;
            _points[7].transform.position = pIndexDip;
            _points[8].transform.position = pIndexTip;
            _points[9].transform.position = pMiddleMcp;
            _points[10].transform.position = pMiddlePip;
            _points[11].transform.position = pMiddleDip;
            _points[12].transform.position = pMiddleTip;
            _points[13].transform.position = pRingMcp;
            _points[14].transform.position = pRingPip;
            _points[15].transform.position = pRingDip;
            _points[16].transform.position = pRingTip;
            _points[17].transform.position = pPinkyMcp;
            _points[18].transform.position = pPinkyPip;
            _points[19].transform.position = pPinkyDip;
            _points[20].transform.position = pPinkyTip;
            _points[21].transform.position = pPalm;

            // Desenha as linhas do "esqueleto"
            _thumb.SetPositions(new[] { pPalm, pThumbCmc, pThumbMcp, pThumbIp, pThumbTip });
            _index.SetPositions(new[] { pPalm, pIndexMcp, pIndexPip, pIndexDip, pIndexTip });
            _middle.SetPositions(new[] { pPalm, pMiddleMcp, pMiddlePip, pMiddleDip, pMiddleTip });
            _ring.SetPositions(new[] { pPalm, pRingMcp, pRingPip, pRingDip, pRingTip });
            _pinky.SetPositions(new[] { pPalm, pPinkyMcp, pPinkyPip, pPinkyDip, pPinkyTip });
            _mcpLine.SetPositions(new[] { pThumbMcp, pIndexMcp, pMiddleMcp, pRingMcp, pPinkyMcp, pRoot, pThumbCmc });
            _palmLine.SetPositions(new[] { pRoot, pPalm });

            // Bounding box só em 2D
            _boundingBox.enabled = _is2D;
            if (_is2D)
            {
                _boundingBox.SetPositions(new[]
                {
                    (Vector3)_image.GetPosition(_hand.BoundingBox2D.min),
                    (Vector3)_image.GetPosition(new Vector2(_hand.BoundingBox2D.xMin, _hand.BoundingBox2D.yMax)),
                    (Vector3)_image.GetPosition(_hand.BoundingBox2D.max),
                    (Vector3)_image.GetPosition(new Vector2(_hand.BoundingBox2D.xMax, _hand.BoundingBox2D.yMin)),
                    (Vector3)_image.GetPosition(_hand.BoundingBox2D.min)
                });
            }

            // ==================================================================
            // Desenha a pinchLine SEMPRE, entre ThumbTip e IndexTip (caso exista)
            // ==================================================================
            if (_pinchLine != null)
            {
                _pinchLine.enabled = true;
                // Garante apenas 2 pontos
                _pinchLine.positionCount = 2;
                // Passa um array de 2 posições
                _pinchLine.SetPositions(new Vector3[] { pThumbTip, pIndexTip });
            }

            // ==================================================================
            // DETECTA PINCH
            // ==================================================================
            _pinchDistance = Vector3.Distance(pThumbTip, pIndexTip);
            bool isPinchNow = _pinchDistance < _pinchThreshold;

            // (Opcional) Raycast em 3D para ver se há algo entre os dedos
            bool somethingBetweenFingers = false;
            if (!_is2D)
            {
                Vector3 direction = pIndexTip - pThumbTip;
                float dist = direction.magnitude;
                if (Physics.Raycast(pThumbTip, direction.normalized, out RaycastHit hit, dist))
                {
                    somethingBetweenFingers = true;
                }
            }

            // Atualiza as cores de polegar, indicador e pinchLine (se pinch ou não)
            UpdatePinchVisual(isPinchNow, somethingBetweenFingers);

            // ==================================================================
            // Armazena a posição de tela para consultas externas (2D Overlay)
            // ==================================================================
            if (_is2D)
            {
                // Caso já sejam coordenadas de tela, apenas convertem para Vector2
                _thumbTipScreenPos = pThumbTip;
                _indexTipScreenPos = pIndexTip;
            }
            else
            {
                // Se estiver em 3D e quiser fornecer "GetPinchCenter()" em termos de SCREEN:
                // Precisamos converter com Camera.main.WorldToScreenPoint(...).
                // EXEMPLO (ajuste se usar outra câmera):
                Vector3 screenThumb = Camera.main.WorldToScreenPoint(pThumbTip);
                Vector3 screenIndex = Camera.main.WorldToScreenPoint(pIndexTip);

                _thumbTipScreenPos = (Vector2)screenThumb;
                _indexTipScreenPos = (Vector2)screenIndex;
            }
        }

        /// <summary>
        /// Ativa ou desativa TODOS os LineRenderers da mão (inclusive a pinchLine).
        /// </summary>
        public void Toggle(bool show)
        {
            _thumb.enabled = show;
            _index.enabled = show;
            _middle.enabled = show;
            _ring.enabled = show;
            _pinky.enabled = show;
            _mcpLine.enabled = show;
            _palmLine.enabled = show;
            _boundingBox.enabled = show;

            if (_pinchLine != null)
            {
                _pinchLine.enabled = show;
            }
        }

        /// <summary>
        /// Muda a cor do polegar, indicador e pinchLine para vermelho se estiver pinçando.
        /// Caso contrário, restaura as cores padrão (chamando Recreate()).
        /// </summary>
        private void UpdatePinchVisual(bool isPinchNow, bool somethingBetweenFingers)
        {
            _isPinching = isPinchNow && !somethingBetweenFingers;

            if (_isPinching)
            {
                // Muda as linhas do polegar e indicador para vermelho
                _thumb.startColor = Color.red;
                _thumb.endColor = Color.red;

                _index.startColor = Color.red;
                _index.endColor = Color.red;

                // Muda a pinchLine para vermelho também
                if (_pinchLine != null)
                {
                    _pinchLine.startColor = Color.red;
                    _pinchLine.endColor = Color.red;
                }
            }
            else
            {
                // Restaura as cores de tudo
                Recreate();
            }
        }

        /// <summary>
        /// Converte coordenadas 2D normalizadas (0..1) para posição na tela/Canvas
        /// </summary>
        private Vector3 GetPosition2D(Vector2 original)
        {
            if (_image == null) return (Vector3)original;
            return (Vector3)_image.GetPosition(original);
        }

        /// <summary>
        /// Converte coordenadas 3D, compensando isFrontFacing se for câmera frontal no mobile.
        /// </summary>
        private Vector3 GetPosition3D(Vector3 original, bool isFrontFacing = false)
        {
            Vector3 result = original + _offset;

            if (!Application.isMobilePlatform)
            {
                return result;
            }

            switch (Screen.orientation)
            {
                case ScreenOrientation.Portrait:
                    return new Vector3(result.y, -result.x, result.z);

                case ScreenOrientation.PortraitUpsideDown:
                    return new Vector3(result.y, -result.x, result.z);

                case ScreenOrientation.LandscapeLeft:
                    {
                        float x = isFrontFacing ? -result.x : result.x;
                        float y = isFrontFacing ? -result.y : result.y;
                        return new Vector3(x, y, result.z);
                    }
                case ScreenOrientation.LandscapeRight:
                    {
                        float x = isFrontFacing ? result.x : -result.x;
                        float y = isFrontFacing ? result.y : -result.y;
                        return new Vector3(x, y, result.z);
                    }
                default:
                    return result;
            }
        }
    }
}
