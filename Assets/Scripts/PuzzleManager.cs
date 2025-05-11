using System.Collections.Generic;
using System.Linq;
using ImageGeneration;
using LM;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PuzzleManager : MonoBehaviour
{
    private const int TextureResolutionPerPiece = 128;

    [Header("Puzzle Settings")] public int rows = 4;

    public int cols = 4;
    public float pieceSize = 1.5f;
    public GameObject puzzlePiecePrefab;

    public GameObject puzzlePieceOutlinePrefab;

    [Header("Layout")] public Transform boardOrigin;

    public Transform scrambleOrigin;

    [Header("UI")] public TextMeshProUGUI timerText;

    public TextMeshProUGUI winText;

    [Header("Audio")] public AudioClip correctPlacementSound;

    public AudioClip incorrectPlacementSound;
    [SerializeField] private float scrambleDistance;
    [SerializeField] private float scramblePositionDistortion = 0.2f;

    [Header("Image Generation")] [SerializeField]
    public ImageProvider imageProvider;

    private readonly HashSet<int> _correctlyPlacedPieceIDs = new();
    private readonly List<PuzzlePiece> _pieces = new();
    private readonly Dictionary<int, Vector3> _targetSlots = new();
    private AudioSource _audioSource;
    private bool _gameActive;
    private bool _gameWon;
    private JointShape[,] _horizontalJoints;
    private Texture2D _sourceTexture;
    private float _startTime;
    private JointShape[,] _verticalJoints;

    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();

        winText.gameObject.SetActive(false);
        GenerateAndSetupPuzzle();

        timerText.outlineWidth = 0.2f;
        timerText.outlineColor = Color.black;

        winText.outlineWidth = 0.2f;
        winText.outlineColor = Color.black;
    }

    private void Update()
    {
        if (!_gameActive || _gameWon) return;

        var t = Time.time - _startTime;
        var minutes = ((int)t / 60).ToString("00");
        var seconds = (t % 60).ToString("00.00");
        timerText.text = "Time: " + minutes + ":" + seconds;
    }

    private void InitializeJoints()
    {
        _horizontalJoints = new JointShape[rows, cols - 1];
        _verticalJoints = new JointShape[rows - 1, cols];

        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols - 1; c++)
            _horizontalJoints[r, c] = Random.value > 0.5f ? JointShape.Knob : JointShape.Indent;

        for (var r = 0; r < rows - 1; r++)
        for (var c = 0; c < cols; c++)
            _verticalJoints[r, c] = Random.value > 0.5f ? JointShape.Knob : JointShape.Indent;
    }

    private PieceEdgeType GetPieceEdgeTypeInternal(int r, int c, int edgeDirection)
    {
        switch (edgeDirection)
        {
            case 0: // Top edge
                if (r == 0) return PieceEdgeType.Flat;
                return _verticalJoints[r - 1, c] == JointShape.Knob ? PieceEdgeType.Indent : PieceEdgeType.Knob;
            case 1: // Right edge
                if (c == cols - 1) return PieceEdgeType.Flat;
                return _horizontalJoints[r, c] == JointShape.Knob ? PieceEdgeType.Knob : PieceEdgeType.Indent;
            case 2: // Bottom edge
                if (r == rows - 1) return PieceEdgeType.Flat;
                return _verticalJoints[r, c] == JointShape.Knob ? PieceEdgeType.Knob : PieceEdgeType.Indent;
            case 3: // Left edge
                if (c == 0) return PieceEdgeType.Flat;
                return _horizontalJoints[r, c - 1] == JointShape.Knob ? PieceEdgeType.Indent : PieceEdgeType.Knob;
            default:
                return PieceEdgeType.Flat;
        }
    }


    private void GenerateAndSetupPuzzle()
    {
        ClearPuzzle();
        InitializeJoints();
        GenerateSourceTexture();
        CreatePuzzlePieces();
        ShuffleAndPlacePieces();

        _startTime = Time.time;
        _gameActive = true;
        _gameWon = false;
        winText.gameObject.SetActive(false);
        timerText.text = "Time: 00:00.00";
        _correctlyPlacedPieceIDs.Clear();
    }

    private void ClearPuzzle()
    {
        foreach (var piece in _pieces)
            Destroy(piece.gameObject);
        _pieces.Clear();
        _targetSlots.Clear();

        foreach (Transform child in boardOrigin)
            if (child.name.StartsWith("Outline_"))
                Destroy(child.gameObject);
    }

    private void GenerateSourceTexture()
    {
        var textureWidth = cols * TextureResolutionPerPiece;
        var textureHeight = rows * TextureResolutionPerPiece;
        _sourceTexture = new Texture2D(textureWidth, textureHeight);

        var pixels = imageProvider.GetImage(textureWidth, textureHeight);

        for (var y = 0; y < textureHeight; y++)
        for (var x = 0; x < textureWidth; x++)
            _sourceTexture.SetPixel(x, y, pixels[x, y]);

        _sourceTexture.Apply();
    }

    private void CreatePuzzlePieces()
    {
        var pieceID = 0;
        const float knobRadiusRatio = 0.25f;
        var knobPixelRadius = (int)(Mathf.Min(TextureResolutionPerPiece, TextureResolutionPerPiece) * knobRadiusRatio);

        var pieceTexWidth = TextureResolutionPerPiece + 2 * knobPixelRadius;
        var pieceTexHeight = TextureResolutionPerPiece + 2 * knobPixelRadius;

        var contentOffsetX = knobPixelRadius;
        var contentOffsetY = knobPixelRadius;

        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
        {
            // 1. Get the source image slice pixels
            var sliceRect = new Rect(c * TextureResolutionPerPiece, (rows - 1 - r) * TextureResolutionPerPiece,
                TextureResolutionPerPiece,
                TextureResolutionPerPiece);
            var sourceSlicePixels = _sourceTexture.GetPixels((int)sliceRect.x, (int)sliceRect.y, (int)sliceRect.width,
                (int)sliceRect.height);

            // 2. Create new texture for this piece, initially transparent
            var pieceTexture = new Texture2D(pieceTexWidth, pieceTexHeight, TextureFormat.RGBA32, false);
            var piecePixels = new Color32[pieceTexWidth * pieceTexHeight];
            for (var i = 0; i < piecePixels.Length; i++) piecePixels[i] = Color.clear;

            // 3. Copy source slice into the content area of the piece texture
            for (var y = 0; y < TextureResolutionPerPiece; y++)
            for (var x = 0; x < TextureResolutionPerPiece; x++)
                piecePixels[(contentOffsetY + y) * pieceTexWidth + contentOffsetX + x] =
                    sourceSlicePixels[y * TextureResolutionPerPiece + x];

            // 4. Determine edge types and draw knobs/indents
            var topEdge = GetPieceEdgeTypeInternal(r, c, 0);
            var rightEdge = GetPieceEdgeTypeInternal(r, c, 1);
            var bottomEdge = GetPieceEdgeTypeInternal(r, c, 2);
            var leftEdge = GetPieceEdgeTypeInternal(r, c, 3);

            // --- Draw Top Knob/Indent ---
            if (topEdge != PieceEdgeType.Flat)
                DrawKnobOrIndent(piecePixels, pieceTexWidth, contentOffsetX + TextureResolutionPerPiece / 2,
                    contentOffsetY + TextureResolutionPerPiece, knobPixelRadius, topEdge, 0, sourceSlicePixels,
                    TextureResolutionPerPiece, TextureResolutionPerPiece);

            // --- Draw Right Knob/Indent ---
            if (rightEdge != PieceEdgeType.Flat)
                DrawKnobOrIndent(piecePixels, pieceTexWidth, contentOffsetX + TextureResolutionPerPiece,
                    contentOffsetY + TextureResolutionPerPiece / 2, knobPixelRadius, rightEdge, 1, sourceSlicePixels,
                    TextureResolutionPerPiece, TextureResolutionPerPiece);

            // --- Draw Bottom Knob/Indent ---
            if (bottomEdge != PieceEdgeType.Flat)
                DrawKnobOrIndent(piecePixels, pieceTexWidth, contentOffsetX + TextureResolutionPerPiece / 2,
                    contentOffsetY,
                    knobPixelRadius, bottomEdge, 2, sourceSlicePixels, TextureResolutionPerPiece,
                    TextureResolutionPerPiece);

            // --- Draw Left Knob/Indent ---
            if (leftEdge != PieceEdgeType.Flat)
                DrawKnobOrIndent(piecePixels, pieceTexWidth, contentOffsetX,
                    contentOffsetY + TextureResolutionPerPiece / 2,
                    knobPixelRadius, leftEdge, 3, sourceSlicePixels, TextureResolutionPerPiece,
                    TextureResolutionPerPiece);

            pieceTexture.SetPixels32(piecePixels);
            pieceTexture.filterMode = FilterMode.Point;
            pieceTexture.Apply();

            // 5. Create Sprite from the generated texture
            var pixelsPerUnit = TextureResolutionPerPiece / pieceSize;
            var finalPieceSprite = Sprite.Create(pieceTexture, new Rect(0, 0, pieceTexWidth, pieceTexHeight),
                new Vector2(0.5f, 0.5f), pixelsPerUnit);

            var pieceGo =
                Instantiate(puzzlePiecePrefab, scrambleOrigin);
            pieceGo.name = $"Piece_{r}_{c}";

            var targetPos = boardOrigin.position + new Vector3(c * pieceSize, -r * pieceSize, 0);

            var pieceScript = pieceGo.GetComponent<PuzzlePiece>();
            pieceScript.Initialize(pieceID, targetPos, Vector3.zero, finalPieceSprite,
                this);

            var outline = Instantiate(puzzlePieceOutlinePrefab, boardOrigin);
            outline.transform.position = targetPos;
            outline.name = $"Outline_{r}_{c}";

            outline.transform.localScale = Vector3.one * pieceSize;

            _pieces.Add(pieceScript);
            _targetSlots.Add(pieceID, targetPos);
            pieceID++;
        }
    }

    private static void DrawKnobOrIndent(Color32[] pixels, int texWidth, int centerX, int centerY, int radius,
        PieceEdgeType type, int edgeDir, Color[] contentPixels, int contentW, int contentH)
    {
        var radiusSquared = radius * radius;

        for (var y = -radius; y <= radius; y++)
        for (var x = -radius; x <= radius; x++)
        {
            if (x * x + y * y > radiusSquared) continue;

            var currentPx = 0;
            var currentPy = 0;

            if (type == PieceEdgeType.Knob)
            {
                switch (edgeDir)
                {
                    // Knob extends outwards
                    case 0:
                    case 1:
                        currentPx = centerX + x;
                        currentPy = centerY + y; // Top or Right
                        break;
                    case 2:
                        currentPx = centerX + x;
                        currentPy = centerY - y; // Bottom (y inverted for outward)
                        break;
                    case 3:
                        currentPx = centerX - x;
                        currentPy = centerY + y; // Left (x inverted for outward)
                        break;
                }

                var isOutwardHalf = (edgeDir == 0 && y >= 0) || (edgeDir == 1 && x >= 0) ||
                                    (edgeDir == 2 && y >= 0) || (edgeDir == 3 && x >= 0);
                if (!isOutwardHalf) continue;
            }
            else // Indent is cut into content
            {
                switch (edgeDir)
                {
                    case 0:
                        currentPx = centerX + x;
                        currentPy = centerY - y; // Top Indent (y inverted for inward)
                        break;
                    case 1:
                        currentPx = centerX - x;
                        currentPy = centerY + y; // Right Indent (x inverted for inward)
                        break;
                    case 2:
                    case 3:
                        currentPx = centerX + x;
                        currentPy = centerY + y; // Bottom or Left Indent
                        break;
                }

                var isOutwardHalf = (edgeDir == 0 && y >= 0) || (edgeDir == 1 && x >= 0) ||
                                    (edgeDir == 2 && y >= 0) || (edgeDir == 3 && x >= 0);
                if (!isOutwardHalf) continue;
            }


            if (currentPx < 0 || currentPx >= texWidth || currentPy < 0 ||
                currentPy >= pixels.Length / texWidth) continue;

            var pixelIndex = currentPy * texWidth + currentPx;
            if (type == PieceEdgeType.Knob)
            {
                // Color the knob: Sample from the edge of the content
                var knobColor = Color.black; // Fallback
                int sampleX = 0, sampleY = 0;
                switch (edgeDir)
                {
                    case 0:
                        sampleX = contentW / 2;
                        sampleY = contentH - 1; // Top edge of content
                        break;
                    case 1:
                        sampleX = contentW - 1;
                        sampleY = contentH / 2; // Right edge
                        break;
                    case 2:
                        sampleX = contentW / 2;
                        sampleY = 0; // Bottom edge
                        break;
                    case 3:
                        sampleX = 0;
                        sampleY = contentH / 2; // Left edge
                        break;
                }

                if (sampleX >= 0 && sampleX < contentW && sampleY >= 0 && sampleY < contentH)
                    knobColor = contentPixels[sampleY * contentW + sampleX];
                pixels[pixelIndex] = knobColor;
            }
            else // Indent
            {
                pixels[pixelIndex] = Color.clear; // Make content transparent
            }
        }
    }


    private void ShuffleAndPlacePieces()
    {
        const float knobPixelRadius = .25f;

        var availableScramblePositions = new List<Vector3>();
        var numScrambleCols = Mathf.CeilToInt(Mathf.Sqrt(_pieces.Count));
        var numScrambleRows = Mathf.CeilToInt((float)_pieces.Count / numScrambleCols);

        var avgPieceActualWidth =
            pieceSize * ((TextureResolutionPerPiece + 2 * knobPixelRadius) / TextureResolutionPerPiece);
        var avgPieceActualHeight =
            pieceSize * ((TextureResolutionPerPiece + 2 * knobPixelRadius) / TextureResolutionPerPiece);


        for (var i = 0; i < _pieces.Count; i++)
        {
            var r = i / numScrambleCols;
            var c = i % numScrambleCols;
            var x = (c - (numScrambleCols - 1) / 2.0f) * avgPieceActualWidth * scrambleDistance;
            var y = -(r - (numScrambleRows - 1) / 2.0f) * avgPieceActualHeight * scrambleDistance;
            availableScramblePositions.Add(scrambleOrigin.position + new Vector3(x, y, 0));
        }

        availableScramblePositions.Shuffle();

        for (var i = 0; i < _pieces.Count; i++)
            if (i < availableScramblePositions.Count)
                _pieces[i].ResetPiece(availableScramblePositions[i] +
                                      (Vector3)Random.insideUnitCircle * scramblePositionDistortion);
    }

    public void PiecePlacedCorrectly(PuzzlePiece piece)
    {
        if (!_correctlyPlacedPieceIDs.Add(piece.pieceID)) return;
        PlaySound(correctPlacementSound);
        if (_correctlyPlacedPieceIDs.Count != _pieces.Count) return;

        _gameActive = false;
        _gameWon = true;
        winText.text = "YOU WIN!\nTime: " + timerText.text[6..];
        winText.gameObject.SetActive(true);
        Debug.Log("Puzzle Solved!");
    }

    public void PiecePlacedIncorrectly()
    {
        PlaySound(incorrectPlacementSound);
    }

    public bool IsSlotTaken(int pieceIdToCheck, Vector3 targetPosition)
    {
        return _pieces.Any(piece =>
            piece.IsPlaced() && piece.pieceID != pieceIdToCheck &&
            Vector3.Distance(piece.transform.position, targetPosition) < 0.1f);
    }

    private void PlaySound(AudioClip clip)
    {
        if (_audioSource && clip) _audioSource.PlayOneShot(clip);
    }

    public bool IsGameActive()
    {
        return _gameActive;
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private enum JointShape
    {
        Knob,
        Indent
    }

    private enum PieceEdgeType
    {
        Flat,
        Knob,
        Indent
    }
}